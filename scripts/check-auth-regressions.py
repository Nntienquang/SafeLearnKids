"""Repeat SQL signup races and probe auth route aliases against the built root app.

Uses the existing smoke harness, disposable UUID accounts and private email pickup.
Build first. No schema changes, SMTP delivery, staging or Git changes are performed.
"""

import argparse
from concurrent.futures import ThreadPoolExecutor
import importlib.util
import json
from pathlib import Path
import re
import socket
import sys
import tempfile
import threading
import uuid


sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("auth_smoke", Path(__file__).with_name("smoke-sql-auth.py"))
smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(smoke)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--race-rounds", type=int, default=16)
    parser.add_argument("--section", choices=("all", "race", "rate"), default="all")
    parser.add_argument("--output", type=Path, help="Optional JSON results; never includes credentials or email tokens.")
    args = parser.parse_args()
    if not 1 <= args.race_rounds <= 100:
        parser.error("--race-rounds must be between 1 and 100")
    results, logs, addresses = [], [], []
    run_id = uuid.uuid4().hex

    def record(ok, label, **details):
        results.append({"pass": bool(ok), "label": label, **details})
        print(f"{'PASS' if ok else 'FAIL'} {label}", flush=True)

    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        url = f"http://127.0.0.1:{listener.getsockname()[1]}"
    try:
        with tempfile.TemporaryDirectory(prefix="safelearn-auth-regression-") as temporary:
            directory = Path(temporary)
            if args.section in ("all", "race"):
                with smoke.running_app(url, directory, logs=logs):
                    with ThreadPoolExecutor(max_workers=2) as executor:
                        for round_number in range(args.race_rounds):
                            address = f"sql-regression-{run_id}-{round_number}@example.invalid"
                            addresses.append(address)
                            password = "Regression1" + uuid.uuid4().hex
                            barrier = threading.Barrier(2)

                            def register(index):
                                client = smoke.Client(url)
                                token = smoke.token_from(client.request("/register")[2])
                                email = address if index == 0 else " " + address.upper() + " "
                                values = {"FullName": "SQL auth regression", "Email": email,
                                          "Password": password, "ConfirmPassword": password,
                                          "Agree": "true", "__RequestVerificationToken": token}
                                barrier.wait(timeout=10)
                                return client.request("/register", values)

                            responses = list(executor.map(register, range(2)))
                            statuses = [response[0] for response in responses]
                            safe_response = all(response[0] == 302 and response[1].get("Location") == "/check-email"
                                                for response in responses)
                            user_count = smoke.sql(f"SELECT COUNT(*) FROM dbo.Users WHERE NormalizedEmail=N'{address.upper()}';")
                            parent_count = smoke.sql(
                                "SELECT COUNT(*) FROM dbo.Users u JOIN dbo.UserRoles ur ON ur.UserId=u.Id "
                                "JOIN dbo.Roles r ON r.Id=ur.RoleId "
                                f"WHERE u.NormalizedEmail=N'{address.upper()}' AND u.EmailConfirmed=0 AND u.IsActive=1 "
                                "AND LEN(u.PasswordHash)>40 AND r.NormalizedName=N'PARENT';")
                            record(safe_response and user_count == parent_count == "1",
                                   f"signup pair {round_number + 1}: generic responses, one pending user with Parent role",
                                   statuses=statuses, user_count=user_count, parent_count=parent_count)
            if args.section in ("all", "rate"):
                with smoke.running_app(url, directory, {"Auth__RateLimitPermitLimit": "2"}, logs) as client:
                    token = smoke.token_from(client.request("/recover")[2])
                    values = {"__RequestVerificationToken": token}
                    for route, expected in (("recover", 200), ("login", 200), ("register", 200),
                                            ("resend-confirmation", 200), ("confirm-email", 200),
                                            ("reset-password", 200), ("change-password", 302), ("logout", 302)):
                        first = client.request("/" + route, values)
                        second = client.request("/" + route, values)
                        record(first[0] == second[0] == expected, f"{route}: independent two-request budget")
                        aliases = ("/" + route, "/" + route.upper(), "/" + route.capitalize(),
                                   "/" + route + "/", "/" + route.upper() + "/",
                                   f"/%{ord(route[0]):02X}" + route[1:])
                        for alias in aliases:
                            status, headers, body = client.request(alias, values, {"X-Forwarded-For": "192.0.2.123"})
                            record(status == 429 and bool(headers.get("Retry-After"))
                                   and headers.get_content_type() == "text/plain"
                                   and headers.get_content_charset() == "utf-8" and "Vui lòng" in body,
                                   f"{route}: exhausted budget applies to {alias}", status=status)
                        read_route = "/check-email" if route == "resend-confirmation" else "/login"
                        record(client.request(read_route)[0] == 200, f"{route}: GET still available after POST limit")
            record(not any("fail: Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware" in log for log in logs),
                   "no unhandled server exception in regression processes")
    finally:
        if addresses:
            # Exact generated addresses AND the test-only marker; no broad prefix deletes.
            address_list = ",".join(f"N'{address}'" for address in addresses)
            address_list = address_list.upper()
            smoke.sql(f"DELETE FROM dbo.Users WHERE NormalizedEmail IN ({address_list}) AND FullName=N'SQL auth regression';")
            remaining = smoke.sql(f"SELECT COUNT(*) FROM dbo.Users WHERE NormalizedEmail IN ({address_list});")
            record(remaining == "0", "all generated regression accounts and Identity links cleaned up")
        summary = {"passed": sum(result["pass"] for result in results), "total": len(results),
                   "race_rounds": len(addresses),
                   "deadlock_log_entries": sum(len(re.findall(r"Error Number:1205", log)) for log in logs),
                   "results": results}
        if args.output:
            args.output.parent.mkdir(parents=True, exist_ok=True)
            args.output.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"RESULT {summary['passed']}/{summary['total']} passed (SQL signup races + auth URL aliases)")
    return 0 if summary["passed"] == summary["total"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
