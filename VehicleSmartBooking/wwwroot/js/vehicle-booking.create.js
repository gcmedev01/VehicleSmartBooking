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
    const availabilityBox = $("#vbAvailability");
    const startAtInput = form?.querySelector("input[name='StartAt']");
    const endAtInput = form?.querySelector("input[name='EndAt']");
    const vehicleTypeInputs = $$('input[name="VehicleType"]');

    // Guard: ถ้าไม่ใช่หน้า Create ก็ออก
    if (!form && !modeHidden && bookingBtns.length === 0) return;

    function setAvailabilityState(message, level) {
        if (!availabilityBox) return;

        availabilityBox.textContent = message;
        availabilityBox.classList.remove("d-none", "alert-success", "alert-warning", "alert-danger", "alert-info");

        if (!level) {
            availabilityBox.classList.add("d-none");
            return;
        }

        availabilityBox.classList.add(`alert-${level}`);
    }

    function getSelectedVehicleType() {
        const checked = vehicleTypeInputs.find(el => el.checked);
        return checked?.value || "";
    }

    async function updateAvailability() {
        if (!availabilityBox) return;

        const vehicleType = getSelectedVehicleType();
        const startAt = startAtInput?.value || "";
        const endAt = endAtInput?.value || "";

        if (!vehicleType || !startAt || !endAt) {
            setAvailabilityState("", null);
            return;
        }

        const startDate = new Date(startAt);
        const endDate = new Date(endAt);
        if (!startDate.getTime() || !endDate.getTime()) {
            setAvailabilityState("กรุณาเลือกวันและเวลาให้ถูกต้อง", "warning");
            return;
        }

        if (endDate <= startDate) {
            setAvailabilityState("เวลาสิ้นสุดต้องมากกว่าเวลาเริ่มต้น", "warning");
            return;
        }

        setAvailabilityState("กำลังตรวจสอบรถว่าง...", "info");

        try {
            const url = `/booking/availability?vehicleType=${encodeURIComponent(vehicleType)}&startAt=${encodeURIComponent(startAt)}&endAt=${encodeURIComponent(endAt)}`;
            const response = await fetch(url, { headers: { "Accept": "application/json" } });
            if (!response.ok) {
                setAvailabilityState("ไม่สามารถตรวจสอบรถว่างได้ในขณะนี้", "warning");
                return;
            }

            const result = await response.json();
            if (!result?.ok) {
                const message = result?.message || "กรุณาตรวจสอบข้อมูลที่กรอก";
                setAvailabilityState(message, "warning");
                return;
            }

            if (result.total === 0) {
                setAvailabilityState("ไม่พบรถบริษัทในประเภทรถนี้", "warning");
                return;
            }

            if (result.available > 0) {
                setAvailabilityState(`มีรถว่าง ${result.available} จาก ${result.total} คันในช่วงเวลาที่เลือก`, "success");
                return;
            }

            setAvailabilityState("ไม่มีรถว่างในช่วงเวลานี้ ระบบจะส่งคำขอไปผู้ให้บริการภายนอก", "danger");
        } catch {
            setAvailabilityState("ไม่สามารถตรวจสอบรถว่างได้ในขณะนี้", "warning");
        }
    }

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
            setAvailabilityState("", null);

            // ถ้าใช้ custom UI ที่ไม่ใช่ native reset เช่น select2 ต้อง reset เพิ่มเองตรงนี้
        });
    }

    vehicleTypeInputs.forEach(el => el.addEventListener("change", updateAvailability));
    if (startAtInput) startAtInput.addEventListener("change", updateAvailability);
    if (endAtInput) endAtInput.addEventListener("change", updateAvailability);

    // ===== init =====
    // ถ้ามีค่าเดิมใน hidden ให้ยึดค่านั้น, ไม่งั้น default "in"
    setBookingMode(modeHidden?.value || "in");
    updateAvailability();
})();
