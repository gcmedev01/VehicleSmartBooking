(function () {
    "use strict";

    const $ = (sel, root = document) => root.querySelector(sel);
    const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));

    const hisSearch = $("#hisSearch");
    const hisVehicle = $("#hisVehicle");
    const hisStatus = $("#hisStatus");
    const hisClear = $("#hisClear");

    const items = $$(".his-row"); // ทั้ง table + card
    const emptyRow = $("#hisEmptyRow");
    const emptyCards = $("#hisCardsEmpty");

    const countAll = $("#hisCountAll");
    const countPending = $("#hisCountPending");
    const countCompleted = $("#hisCountCompleted");
    const countBad = $("#hisCountBad");

    if (!hisSearch && !hisVehicle && !hisStatus && items.length === 0) return;

    const COMPLETED_SET = new Set(["Completed", "Rated"]);
    const BAD_SET = new Set(["Rejected", "Cancelled"]);

    function norm(s) {
        return (s || "").toString().trim().toLowerCase();
    }

    function isPendingStatus(status) {
        return !COMPLETED_SET.has(status) && !BAD_SET.has(status);
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
        let all = 0, pending = 0, completed = 0, bad = 0;

        groups.forEach(els => {
            const st = (els[0].getAttribute("data-status") || "").trim();
            all++;

            if (COMPLETED_SET.has(st)) completed++;
            else if (BAD_SET.has(st)) bad++;
            else if (isPendingStatus(st)) pending++;
        });

        if (countAll) countAll.textContent = String(all);
        if (countPending) countPending.textContent = String(pending);
        if (countCompleted) countCompleted.textContent = String(completed);
        if (countBad) countBad.textContent = String(bad);
    }

    function updateList() {
        const q = norm(hisSearch?.value);
        const st = (hisStatus?.value || "all").trim();
        const vehicle = (hisVehicle?.value || "all").trim();

        const groups = groupByBookingId();
        let visibleBookings = 0;

        groups.forEach(els => {
            const status = (els[0].getAttribute("data-status") || "").trim();
            const text = norm(els[0].getAttribute("data-text"));
            const vehicleType = (els[0].getAttribute("data-vehicle") || "").trim();

            const passText = !q || text.includes(q);
            const passStatus = (st === "all") || (status === st);
            const passVehicle = (vehicle === "all") || (vehicleType === vehicle);
            const show = passText && passStatus && passVehicle;

            els.forEach(el => (el.style.display = show ? "" : "none"));
            if (show) visibleBookings++;
        });

        if (emptyRow) emptyRow.style.display = (visibleBookings === 0) ? "" : "none";
        if (emptyCards) emptyCards.style.display = (visibleBookings === 0) ? "" : "none";
    }

    [hisSearch, hisVehicle, hisStatus].forEach(el => {
        if (!el) return;
        el.addEventListener("input", updateList);
        el.addEventListener("change", updateList);
    });

    if (hisClear) {
        hisClear.addEventListener("click", () => {
            if (hisSearch) hisSearch.value = "";
            if (hisVehicle) hisVehicle.value = "all";
            if (hisStatus) hisStatus.value = "all";
            updateList();
        });
    }

    // init
    updateStats();
    updateList();
})();
