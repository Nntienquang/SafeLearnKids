/* SafeLearn Kids · lesson PDFs rendered inside the page with PDF.js.
   The child never leaves the lesson, so the study-time heartbeat keeps counting while they read.
   Pages are drawn when scrolled into view; PDF scripts never run (isEvalSupported:false, no viewer scripting). */
const BASE = '/vendor/pdfjs-4.10.38/';
let library = null;

async function pdfjs() {
  if (!library) {
    library = await import(BASE + 'pdf.min.mjs');
    library.GlobalWorkerOptions.workerSrc = BASE + 'pdf.worker.min.mjs';
  }
  return library;
}

function button(label, text) {
  const b = document.createElement('button');
  b.type = 'button';
  b.className = 'sl-btn sl-btn-sm';
  b.setAttribute('aria-label', label);
  b.textContent = text;
  return b;
}

async function mount(box) {
  const url = box.dataset.pdf;
  const status = box.querySelector('[data-pdf-status]');
  const pages = box.querySelector('[data-pdf-pages]');
  const bar = box.querySelector('[data-pdf-bar]');
  try {
    const lib = await pdfjs();
    const doc = await lib.getDocument({ url, isEvalSupported: false, enableXfa: false }).promise;
    const count = Math.min(doc.numPages, 80);
    let zoom = 1;
    status.textContent = doc.numPages + ' trang' + (doc.numPages > count ? ' (hiển thị ' + count + ' trang đầu)' : '');
    const slots = [];
    const draw = async (slot) => {
      const page = await doc.getPage(slot.number);
      const base = page.getViewport({ scale: 1 });
      const width = Math.max(240, pages.clientWidth - 8) * zoom;
      const viewport = page.getViewport({ scale: width / base.width });
      const ratio = window.devicePixelRatio || 1;
      const canvas = document.createElement('canvas');
      canvas.width = Math.floor(viewport.width * ratio);
      canvas.height = Math.floor(viewport.height * ratio);
      canvas.style.width = Math.floor(viewport.width) + 'px';
      canvas.style.height = Math.floor(viewport.height) + 'px';
      canvas.setAttribute('role', 'img');
      canvas.setAttribute('aria-label', 'Trang ' + slot.number);
      await page.render({ canvasContext: canvas.getContext('2d'), viewport, transform: ratio !== 1 ? [ratio, 0, 0, ratio, 0, 0] : null }).promise;
      slot.el.replaceChildren(canvas);
      slot.drawn = zoom;
    };
    const watcher = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        const slot = slots.find((item) => item.el === entry.target);
        if (entry.isIntersecting && slot && slot.drawn !== zoom) draw(slot).catch(() => { slot.el.textContent = 'Không vẽ được trang ' + slot.number; });
      });
    }, { root: pages, rootMargin: '400px 0px' });
    for (let number = 1; number <= count; number++) {
      const el = document.createElement('div');
      el.className = 'pdf-page';
      el.textContent = 'Trang ' + number;
      pages.appendChild(el);
      const slot = { number, el, drawn: 0 };
      slots.push(slot);
      watcher.observe(el);
    }
    const redraw = () => slots.forEach((slot) => { slot.drawn = 0; watcher.unobserve(slot.el); watcher.observe(slot.el); });
    const smaller = button('Thu nhỏ', '−');
    const bigger = button('Phóng to', '+');
    const full = button('Xem toàn màn hình', '⛶ Toàn màn hình');
    smaller.addEventListener('click', () => { zoom = Math.max(0.6, zoom - 0.2); redraw(); });
    bigger.addEventListener('click', () => { zoom = Math.min(2.4, zoom + 0.2); redraw(); });
    // Fullscreen stays inside this page, so the lesson keeps counting study time.
    full.addEventListener('click', () => {
      if (document.fullscreenElement) document.exitFullscreen();
      else if (box.requestFullscreen) box.requestFullscreen().then(() => setTimeout(redraw, 200)).catch(() => {});
    });
    document.addEventListener('fullscreenchange', () => { if (!document.fullscreenElement) setTimeout(redraw, 200); });
    bar.append(smaller, bigger, full);
    box.dataset.ready = 'true';
  } catch (error) {
    // Old browsers or a damaged file: fall back to the browser's own viewer, still inside the page.
    status.textContent = 'Trình xem chưa mở được tài liệu, đang dùng trình xem của trình duyệt.';
    const frame = document.createElement('iframe');
    frame.src = url;
    frame.title = box.dataset.title || 'Tài liệu';
    frame.className = 'pdf-frame';
    pages.replaceChildren(frame);
    box.dataset.ready = 'fallback';
  }
}

document.querySelectorAll('[data-pdf]').forEach((box) => { mount(box); });
