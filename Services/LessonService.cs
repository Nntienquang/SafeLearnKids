using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

internal static class SqlErrors
{
    public const int ForeignKeyViolation = 547;
    public const int UniqueIndexViolation = 2601;
    public const int UniqueConstraintViolation = 2627;

    public static int? Number(Exception exception) => (exception.GetBaseException() as SqlException)?.Number;

    public static bool IsDuplicate(Exception exception) => Number(exception) is UniqueIndexViolation or UniqueConstraintViolation;
}
