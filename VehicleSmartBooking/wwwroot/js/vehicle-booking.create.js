(function () {
    "use strict";

    const $ = (sel, root = document) => root.querySelector(sel);
    const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));

    // ===== Elements (Create page) =====
    const form = $("#vbForm");
    const modeHidden = $("#BookingMode");
    const bookingBtns = $$("[data-vb-booking]");

    const outExtra = $("#vbOutExtra");
    const personalBox = $("#vbPersonalPlaceholder");
    const outTrip = $("#OutTripType");

    const btnReset = $("#btnReset");

    // Guard: ถ้าไม่ใช่หน้า Create ก็ออก
    if (!form && !modeHidden && bookingBtns.length === 0) return;

    function setBookingMode(mode) {
        const m = (mode || "in").toLowerCase();

        // set active state on toggle buttons
        bookingBtns.forEach(btn => {
            btn.classList.toggle("is-active", btn.getAttribute("data-vb-booking") === m);
        });

        // set hidden input
        if (modeHidden) modeHidden.value = m;

        // toggle sections
        if (outExtra) outExtra.style.display = (m === "out") ? "block" : "none";
        if (personalBox) personalBox.style.display = (m === "personal") ? "block" : "none";

        // require outTrip only when out
        if (outTrip) {
            if (m === "out") outTrip.setAttribute("required", "required");
            else outTrip.removeAttribute("required");
        }

        // NOTE: ถ้าอนาคตอยาก disable fields เมื่อ personal ให้ทำตรงนี้ได้
        // const disable = (m === "personal");
        // if (form) $$("input,select,textarea", form).forEach(el => { ... });
    }

    // Click on booking mode buttons
    document.addEventListener("click", (e) => {
        const btn = e.target.closest("[data-vb-booking]");
        if (!btn) return;

        e.preventDefault();
        const mode = btn.getAttribute("data-vb-booking");
        setBookingMode(mode);
    });

    // Reset button
    if (btnReset) {
        btnReset.addEventListener("click", (e) => {
            e.preventDefault();

            if (form) form.reset();

            // reset mode to "in"
            setBookingMode("in");

            // ถ้าใช้ custom UI ที่ไม่ใช่ native reset เช่น select2 ต้อง reset เพิ่มเองตรงนี้
        });
    }

    // ===== init =====
    // ถ้ามีค่าเดิมใน hidden ให้ยึดค่านั้น, ไม่งั้น default "in"
    setBookingMode(modeHidden?.value || "in");
})();
