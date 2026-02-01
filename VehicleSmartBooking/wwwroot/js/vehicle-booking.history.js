(function () {
    "use strict";

    const $ = (sel, root = document) => root.querySelector(sel);
    const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));

    const hisSearch = $("#hisSearch");
    const hisRange = $("#hisRange");
    const hisStatus = $("#hisStatus");
    const hisClear = $("#hisClear");

    const items = $$(".his-row"); // ทั้ง table + card
    const emptyRow = $("#hisEmptyRow");
    const emptyCards = $("#hisCardsEmpty");

    const countAll = $("#hisCountAll");
    const countPending = $("#hisCountPending");
    const countApproved = $("#hisCountApproved");
    const countBad = $("#hisCountBad");

    if (!hisSearch && !hisRange && !hisStatus && items.length === 0) return;

    const PENDING_SET = new Set(["Submitted", "WaitingApproval"]);
    const APPROVED_SET = new Set(["Approved", "Completed"]);
    const BAD_SET = new Set(["Rejected", "Cancelled"]);

    function parseDateYYYYMMDD(s) {
        if (!s) return null;
        const d = new Date(`${s}T00:00:00`);
        return isNaN(d.getTime()) ? null : d;
    }

    function withinRange(itemDate, rangeVal) {
        if (!itemDate) return true;
        if (!rangeVal || rangeVal === "all") return true;

        const days = parseInt(rangeVal, 10);
        if (isNaN(days)) return true;

        const now = new Date();
        const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        const cutoff = new Date(todayStart);
        cutoff.setDate(todayStart.getDate() - days);

        return itemDate >= cutoff;
    }

    function norm(s) {
        return (s || "").toString().trim().toLowerCase();
    }

    // รวม element ที่เป็น booking เดียวกัน (desktop+mobile) เข้าเป็นกลุ่มเดียว
    function groupByBookingId() {
        const map = new Map(); // id -> [elements]
        items.forEach(el => {
            const id = el.getAttribute("data-booking-id") || "";
            const key = id || (el.getAttribute("data-text") || ""); // fallback กันพัง
            if (!map.has(key)) map.set(key, []);
            map.get(key).push(el);
        });
        return map;
    }

    function updateStats() {
        const groups = groupByBookingId();
        let all = 0, pending = 0, approved = 0, bad = 0;

        groups.forEach(els => {
            // หยิบ status จากตัวแรกพอ (ค่าทุกตัวควรเหมือนกัน)
            const st = (els[0].getAttribute("data-status") || "").trim();
            all++;

            if (PENDING_SET.has(st)) pending++;
            else if (APPROVED_SET.has(st)) approved++;
            else if (BAD_SET.has(st)) bad++;
        });

        if (countAll) countAll.textContent = String(all);
        if (countPending) countPending.textContent = String(pending);
        if (countApproved) countApproved.textContent = String(approved);
        if (countBad) countBad.textContent = String(bad);
    }

    function updateList() {
        const q = norm(hisSearch?.value);
        const st = (hisStatus?.value || "all").trim();
        const rg = (hisRange?.value || "30").trim();

        const groups = groupByBookingId();
        let visibleBookings = 0;

        groups.forEach(els => {
            const status = (els[0].getAttribute("data-status") || "").trim();
            const text = norm(els[0].getAttribute("data-text"));
            const dateStr = (els[0].getAttribute("data-date") || "").trim();
            const date = parseDateYYYYMMDD(dateStr);

            const passText = !q || text.includes(q);
            const passStatus = (st === "all") || (status === st);
            const passRange = withinRange(date, rg);
            const show = passText && passStatus && passRange;

            els.forEach(el => (el.style.display = show ? "" : "none"));
            if (show) visibleBookings++;
        });

        if (emptyRow) emptyRow.style.display = (visibleBookings === 0) ? "" : "none";
        if (emptyCards) emptyCards.style.display = (visibleBookings === 0) ? "" : "none";
    }

    [hisSearch, hisRange, hisStatus].forEach(el => {
        if (!el) return;
        el.addEventListener("input", updateList);
        el.addEventListener("change", updateList);
    });

    if (hisClear) {
        hisClear.addEventListener("click", () => {
            if (hisSearch) hisSearch.value = "";
            if (hisRange) hisRange.value = "30";
            if (hisStatus) hisStatus.value = "all";
            updateList();
        });
    }

    // init
    updateStats();
    updateList();
})();
