(function () {
    "use strict";

    const $ = (sel, root = document) => root.querySelector(sel);
    const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));

    // ====== Section navigation (sidebar) ======
    function setActiveSection(hash) {
        const target = hash && hash.startsWith("#") ? hash : "#sec-booking";
        const sections = $$(".vb-section");
        const navItems = $$("[data-vb-nav]");

        sections.forEach(s => s.classList.toggle("is-active", ("#" + s.id) === target));
        navItems.forEach(n => n.classList.toggle("is-active", n.getAttribute("data-vb-nav") === target));

        // close offcanvas on mobile
        const offcanvasEl = $("#vbSidebar");
        if (offcanvasEl && offcanvasEl.classList.contains("show")) {
            const bsOffcanvas = bootstrap.Offcanvas.getInstance(offcanvasEl);
            if (bsOffcanvas) bsOffcanvas.hide();
        }

        // keep URL hash
        if (location.hash !== target) history.replaceState(null, "", target);
    }

    document.addEventListener("click", (e) => {
        const nav = e.target.closest("[data-vb-nav]");
        if (!nav) return;
        setActiveSection(nav.getAttribute("data-vb-nav"));
    });

    window.addEventListener("hashchange", () => setActiveSection(location.hash));

    // ====== Booking mode toggle (in/out/personal) ======
    const modeHidden = $("#BookingMode");
    const outExtra = $("#vbOutExtra");
    const personal = $("#vbPersonalPlaceholder");
    const bookingBtns = $$("[data-vb-booking]");

    function setBookingMode(mode) {
        // UI active state
        bookingBtns.forEach(b => b.classList.toggle("is-active", b.getAttribute("data-vb-booking") === mode));

        if (modeHidden) modeHidden.value = mode;

        // Show/hide out-of-province extras
        if (outExtra) outExtra.style.display = (mode === "out") ? "block" : "none";

        // Show/hide personal placeholder
        if (personal) personal.style.display = (mode === "personal") ? "block" : "none";

        // Optional: require OutTripType only when out
        const outTrip = $("#OutTripType");
        if (outTrip) {
            if (mode === "out") outTrip.setAttribute("required", "required");
            else outTrip.removeAttribute("required");
        }

        // Optional: disable form fields when personal (เว้นไว้ก่อน)
        const form = $("#vbForm");
        if (form) {
            const disable = (mode === "personal");
            // ยังไม่ disable ทั้งหมดเพื่อให้แก้ต่อได้ง่าย
            // ถ้าอยาก disable จริง ให้เปิดบล็อคนี้:
            // $$("input,select,textarea", form).forEach(el => {
            //   if (el.id === "BookingMode") return;
            //   if (el.type === "file") return;
            //   el.disabled = disable;
            // });
        }
    }

    document.addEventListener("click", (e) => {
        const btn = e.target.closest("[data-vb-booking]");
        if (!btn) return;
        setBookingMode(btn.getAttribute("data-vb-booking"));
    });

    // Reset button
    const btnReset = $("#btnReset");
    if (btnReset) {
        btnReset.addEventListener("click", () => {
            const form = $("#vbForm");
            if (form) form.reset();
            setBookingMode("in");
        });
    }

    // Initial state
    setActiveSection(location.hash || "#sec-booking");
    setBookingMode("in");

    // ====== History filter/search (client-side demo) ======
    const hisSearch = $("#hisSearch");
    const hisRange = $("#hisRange");
    const hisStatus = $("#hisStatus");
    const hisClear = $("#hisClear");

    const hisRows = $$(".his-row"); // ทั้ง table row + mobile card ใช้ class เดียวกัน
    const hisEmptyRow = $("#hisEmptyRow");
    const hisCardsEmpty = $("#hisCardsEmpty");

    const countAll = $("#hisCountAll");
    const countPending = $("#hisCountPending");
    const countApproved = $("#hisCountApproved");
    const countBad = $("#hisCountBad");

    function parseDateYYYYMMDD(s) {
        // s: "2026-01-28"
        const d = new Date(s + "T00:00:00");
        return isNaN(d.getTime()) ? null : d;
    }

    function withinRange(itemDate, rangeVal) {
        if (!itemDate) return true;
        if (rangeVal === "all") return true;
        const days = parseInt(rangeVal, 10);
        if (isNaN(days)) return true;

        const now = new Date();
        const cutoff = new Date(now.getFullYear(), now.getMonth(), now.getDate() - days);
        return itemDate >= cutoff;
    }

    function updateHistory() {
        const q = (hisSearch?.value || "").trim().toLowerCase();
        const st = hisStatus?.value || "all";
        const rg = hisRange?.value || "30";

        let visibleCount = 0;
        let pending = 0, approved = 0, bad = 0;

        // count all (before filter) based on demo rows
        const all = hisRows.length;

        hisRows.forEach(el => {
            const status = (el.getAttribute("data-status") || "").trim();
            const text = (el.getAttribute("data-text") || "").toLowerCase();
            const dateStr = el.getAttribute("data-date");
            const date = parseDateYYYYMMDD(dateStr);

            const passText = !q || text.includes(q);
            const passStatus = (st === "all") || (status === st);
            const passRange = withinRange(date, rg);

            const show = passText && passStatus && passRange;
            el.style.display = show ? "" : "none";

            if (show) visibleCount++;

            // stats by status (based on all, not filtered) — ถ้าอยากให้นับตาม filter เปลี่ยนเงื่อนไขเป็น show
            if (status === "Pending") pending++;
            if (status === "Approved") approved++;
            if (status === "Rejected" || status === "Cancelled") bad++;
        });

        // empty states
        if (hisEmptyRow) hisEmptyRow.style.display = (visibleCount === 0) ? "" : "none";
        if (hisCardsEmpty) hisCardsEmpty.style.display = (visibleCount === 0) ? "" : "none";

        if (countAll) countAll.textContent = String(all);
        if (countPending) countPending.textContent = String(pending);
        if (countApproved) countApproved.textContent = String(approved);
        if (countBad) countBad.textContent = String(bad);
    }

    [hisSearch, hisRange, hisStatus].forEach(el => {
        if (!el) return;
        el.addEventListener("input", updateHistory);
        el.addEventListener("change", updateHistory);
    });

    if (hisClear) {
        hisClear.addEventListener("click", () => {
            if (hisSearch) hisSearch.value = "";
            if (hisRange) hisRange.value = "30";
            if (hisStatus) hisStatus.value = "all";
            updateHistory();
        });
    }

    // run once (in case user opens history first)
    updateHistory();

    const qStartEl = $("#qStart");
    const qEndEl = $("#qEnd");

    function timeToMin(t) { // "HH:MM"
        if (!t) return 0;
        const [h, m] = t.split(":").map(n => parseInt(n, 10));
        return (h * 60) + (m || 0);
    }

    function overlaps(aStart, aEnd, bStart, bEnd) {
        // overlap if start < otherEnd && otherStart < end
        return aStart < bEnd && bStart < aEnd;
    }

    function getQueryRange() {
        const start = timeToMin(qStartEl?.value || "08:00");
        const end = timeToMin(qEndEl?.value || "17:00");
        // กัน input กลับหัว
        if (end <= start) return { start, end: start + 60 }; // บังคับอย่างน้อย 1 ชม.
        return { start, end };
    }

    // คืนค่าจำนวนรถที่ "ว่างตลอดช่วง" (สำคัญ)
    function freeVehiclesForRange(dateStr, type, range) {
        const fleet = fleetByType[type] || [];
        if (fleet.length === 0) return { free: 0, total: 0, freeList: [] };

        const dayBk = getDayBookings(dateStr, type);

        // รถคันไหนที่มี booking overlap ช่วง [range.start, range.end] ถือว่าไม่ว่าง
        const blocked = new Set();
        for (const b of dayBk) {
            const bs = timeToMin(b.start);
            const be = timeToMin(b.end);
            if (overlaps(range.start, range.end, bs, be)) {
                blocked.add(b.vehicle);
            }
        }

        const freeList = fleet.filter(v => !blocked.has(v));
        return { free: freeList.length, total: fleet.length, freeList };
    }

    function availabilityLevelByRatio(free, total) {
        if (total === 0) return "busy";

        // fleet เล็กให้ใช้ rule discrete จะสมเหตุสมผลกว่า
        if (total <= 3) {
            if (free === total) return "free";     // ว่างเยอะ
            if (free >= 1) return "mid";           // ว่างปานกลาง
            return "busy";                         // ว่างน้อย/เต็ม
        }

        const ratio = free / total;
        if (ratio >= 0.60) return "free";
        if (ratio >= 0.30) return "mid";
        return "busy";
    }

    // ===== Queue (Calendar + Day timeline) =====
    const calTitle = $("#calTitle");
    const calGrid = $("#calGrid");
    const calPrev = $("#calPrev");
    const calNext = $("#calNext");
    const daySubtitle = $("#daySubtitle");
    const daySummary = $("#daySummary");
    const dayTimeline = $("#dayTimeline");
    const typeBtns = $$("[data-qtype]");

    // Mock fleet + bookings (replace with DB later)
    const fleetByType = {
        pickup: ["PU-01", "PU-02", "PU-03"],
        van: ["VAN-01", "VAN-02"],
        sedan: ["SED-01", "SED-02", "SED-03", "SED-04"]
    };

    // booking: date YYYY-MM-DD, vehicleId, start "HH:MM", end "HH:MM", route, purpose
    const bookings = [
        { date: "2026-01-28", type: "van", vehicle: "VAN-01", start: "09:00", end: "12:00", route: "โรงงาน A → บริษัท B", purpose: "ประชุมลูกค้า" },
        { date: "2026-01-28", type: "van", vehicle: "VAN-02", start: "10:30", end: "15:00", route: "โรงงาน A → ท่าเรือ", purpose: "ส่งเอกสาร" },
        { date: "2026-01-20", type: "sedan", vehicle: "SED-02", start: "06:30", end: "19:00", route: "ระยอง → กรุงเทพฯ", purpose: "ติดตั้งงาน" },
        { date: "2026-01-20", type: "sedan", vehicle: "SED-03", start: "08:00", end: "11:30", route: "โรงงาน A → นิคม", purpose: "ตรวจงาน" },
        { date: "2026-01-10", type: "pickup", vehicle: "PU-01", start: "13:00", end: "17:00", route: "โรงงาน A → ท่าเรือ", purpose: "ขนของ" },
        { date: "2026-01-12", type: "pickup", vehicle: "PU-02", start: "09:00", end: "18:00", route: "โรงงาน A → ไซต์งาน", purpose: "ขนเครื่องมือ" },
    ];

    let qType = "pickup";
    let viewYear = 2026;
    let viewMonth = 0; // 0=Jan
    let selectedDate = null; // "YYYY-MM-DD"

    function pad2(n) { return String(n).padStart(2, "0"); }
    function ymd(y, m, d) { return `${y}-${pad2(m + 1)}-${pad2(d)}`; }
    function formatThaiDate(ymdStr) {
        const [y, mo, da] = ymdStr.split("-").map(x => parseInt(x, 10));
        return `${pad2(da)}/${pad2(mo)}/${y}`;
    }

    function monthName(y, m) {
        const dt = new Date(y, m, 1);
        return dt.toLocaleString("en-US", { month: "long", year: "numeric" });
    }

    function getDayBookings(dateStr, type) {
        return bookings.filter(b => b.date === dateStr && b.type === type);
    }

    function availabilityLevel(dateStr, type) {
        const range = getQueryRange();   // 08:00–17:00 หรือที่ user เลือก
        const av = freeVehiclesForRange(dateStr, type, range);

        return {
            free: av.free,
            total: av.total,
            level: availabilityLevelByRatio(av.free, av.total)
        };
    }


    function renderCalendar() {
        if (!calGrid) return;
        calGrid.innerHTML = "";

        // DOW header
        const dows = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
        dows.forEach(d => {
            const el = document.createElement("div");
            el.className = "vb-cal-dow";
            el.textContent = d;
            calGrid.appendChild(el);
        });

        if (calTitle) calTitle.textContent = monthName(viewYear, viewMonth);

        const first = new Date(viewYear, viewMonth, 1);
        const last = new Date(viewYear, viewMonth + 1, 0);
        const startDow = first.getDay();
        const daysInMonth = last.getDate();

        // previous month padding
        const prevLast = new Date(viewYear, viewMonth, 0).getDate();
        for (let i = 0; i < startDow; i++) {
            const day = prevLast - (startDow - 1 - i);
            const cell = buildCell(viewYear, viewMonth - 1, day, true);
            calGrid.appendChild(cell);
        }

        // current month days
        for (let d = 1; d <= daysInMonth; d++) {
            const cell = buildCell(viewYear, viewMonth, d, false);
            calGrid.appendChild(cell);
        }

        // next month padding to fill grid nicely (up to 6 rows)
        const cellsAfterHeader = startDow + daysInMonth;
        const remainder = cellsAfterHeader % 7;
        const need = remainder === 0 ? 0 : (7 - remainder);
        for (let d = 1; d <= need; d++) {
            const cell = buildCell(viewYear, viewMonth + 1, d, true);
            calGrid.appendChild(cell);
        }
    }

    function buildCell(y, m, d, muted) {
        const dt = new Date(y, m, d);
        const yy = dt.getFullYear();
        const mm = dt.getMonth();
        const dd = dt.getDate();
        const dateStr = ymd(yy, mm, dd);

        const av = availabilityLevel(dateStr, qType);
        const dotClass = av.level === "free" ? "vb-dot-free" : (av.level === "mid" ? "vb-dot-mid" : "vb-dot-busy");

        const cell = document.createElement("div");
        cell.className = "vb-cal-cell" + (muted ? " is-muted" : "");
        if (selectedDate === dateStr) cell.classList.add("is-selected");
        cell.setAttribute("data-date", dateStr);

        cell.innerHTML = `
  <div class="vb-cal-day">${dd}</div>
  <div class="vb-cal-mini">${av.free}/${av.total} ว่าง</div>
  <span class="vb-cal-dot ${dotClass}"></span>
`;

        cell.addEventListener("click", () => {
            selectedDate = dateStr;
            renderCalendar();
            renderDayDetail();
        });

        return cell;
    }

    function renderDayDetail() {
        if (!dayTimeline || !daySubtitle || !daySummary) return;

        if (!selectedDate) {
            daySubtitle.textContent = "เลือกวันที่จากปฏิทิน";
            daySummary.innerHTML = "";
            dayTimeline.innerHTML = `<div class="text-muted">ยังไม่ได้เลือกวันที่</div>`;
            return;
        }

        const fleet = fleetByType[qType] || [];
        const dayBk = getDayBookings(selectedDate, qType);   // ✅ ต้องมี
        const range = getQueryRange();

        const av = freeVehiclesForRange(selectedDate, qType, range);

        const startLabel = (qStartEl?.value || "08:00");
        const endLabel = (qEndEl?.value || "17:00");

        daySubtitle.textContent =
            `${formatThaiDate(selectedDate)} • ประเภทรถ: ${qType.toUpperCase()} • ช่วงเวลา ${startLabel}–${endLabel}`;

        daySummary.innerHTML = `
    <span class="vb-summary-pill">ทั้งหมด <b>${av.total}</b> คัน</span>
    <span class="vb-summary-pill">ว่าง <b>${av.free}</b> คัน</span>
    <span class="vb-summary-pill">ไม่ว่าง <b>${av.total - av.free}</b> คัน</span>
  `;

        let html = "";

        if (fleet.length === 0) {
            dayTimeline.innerHTML = `<div class="text-muted">ไม่มีข้อมูลรถในประเภทรถนี้</div>`;
            return;
        }

        fleet.forEach(v => {
            const items = dayBk
                .filter(b => b.vehicle === v)
                .sort((a, b) => a.start.localeCompare(b.start));

            // booked เฉพาะช่วงเวลาที่เลือก
            const isBookedInRange = items.some(x =>
                overlaps(range.start, range.end, timeToMin(x.start), timeToMin(x.end))
            );

            const isFree = !isBookedInRange;

            // แสดงเฉพาะรายการที่ทับช่วงเวลาที่เลือก
            const itemsInRange = items.filter(x =>
                overlaps(range.start, range.end, timeToMin(x.start), timeToMin(x.end))
            );

            html += `
      <div class="vb-vehicle-line">
        <div class="vb-vehicle-head">
          <div>
            <div class="vb-vehicle-name">${v}</div>
            <div class="vb-vehicle-meta">${isFree ? "ว่างในช่วงเวลาที่เลือก" : `${itemsInRange.length} รายการที่ทับช่วงเวลา`}</div>
          </div>
          <span class="vb-status ${isFree ? "vb-status-completed" : "vb-status-pending"}">
            ${isFree ? "Available" : "Booked"}
          </span>
        </div>

        <div class="vb-timeline">
          ${isFree
                    ? `<div class="text-muted small">ไม่มีการจองในช่วง ${startLabel}–${endLabel}</div>`
                    : itemsInRange.map(x => `
                  <div class="vb-booking-block">
                    <div class="vb-booking-time">${x.start} - ${x.end}</div>
                    <div class="vb-booking-desc">${x.route} • ${x.purpose}</div>
                  </div>
                `).join("")
                }
        </div>
      </div>
    `;
        });

        dayTimeline.innerHTML = html;
    }


    function setQueueType(type) {
        qType = type;

        // active state
        typeBtns.forEach(b => b.classList.toggle("is-active", b.getAttribute("data-qtype") === type));

        // re-render
        renderCalendar();
        renderDayDetail();
    }

    // wire buttons
    typeBtns.forEach(b => b.addEventListener("click", () => setQueueType(b.getAttribute("data-qtype"))));

    if (calPrev) calPrev.addEventListener("click", () => {
        viewMonth -= 1;
        if (viewMonth < 0) { viewMonth = 11; viewYear -= 1; }
        renderCalendar();
    });

    if (calNext) calNext.addEventListener("click", () => {
        viewMonth += 1;
        if (viewMonth > 11) { viewMonth = 0; viewYear += 1; }
        renderCalendar();
    });

    // init
    setQueueType("pickup");
    selectedDate = "2026-01-28"; // demo default
    renderCalendar();
    renderDayDetail();
    [qStartEl, qEndEl].forEach(el => {
        if (!el) return;
        el.addEventListener("change", () => {
            renderCalendar();
            renderDayDetail();
        });
    });
})();
