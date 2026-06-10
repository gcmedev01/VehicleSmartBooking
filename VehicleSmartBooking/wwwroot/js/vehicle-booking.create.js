(function () {
    "use strict";

    const $ = (sel, root = document) => root.querySelector(sel);
    const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));

    // ===== Elements (Create page) =====
    const form = $("#vbForm");
    const modeHidden = $("#BookingMode");
    const bookingBtns = $$("[data-vb-booking]");

    const outExtra = $("#vbOutExtra");
    const outTrip = $("#OutTripType");
    const specialOccasionType = $("#SpecialOccasionType");
    const specialOccasionRemark = $("#SpecialOccasionRemark");
    const approvalPreview = $("#vbApprovalPreview");

    const btnReset = $("#btnReset");
    const availabilityBox = $("#vbAvailability");
    const startAtInput = form?.querySelector("input[name='StartAt']");
    const endAtInput = form?.querySelector("input[name='EndAt']");
    const vehicleTypeInputs = $$('input[name="VehicleType"]');
    const serviceOptionInput = $("#ServiceOption");
    const choiceModalEl = $("#vbServiceChoiceModal");
    const choiceButtons = $$('[data-vb-choice]');

    let lastAvailability = null;
    let isSubmitting = false;

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

    function isElectricVehicle(vehicleType) {
        return (vehicleType || "").toLowerCase() === "electric";
    }

    function isSpecialOccasionSelected() {
        const value = (specialOccasionType?.value || "").toLowerCase();
        return value === "wedding" || value === "ordination";
    }

    async function updateAvailability() {
        if (!availabilityBox) return;

        const vehicleType = getSelectedVehicleType();
        const startAt = startAtInput?.value || "";
        const endAt = endAtInput?.value || "";
        const bookingMode = (modeHidden?.value || "in").toLowerCase();

        if (!vehicleType || !startAt || !endAt) {
            lastAvailability = null;
            if (serviceOptionInput) serviceOptionInput.value = "";
            setAvailabilityState("", null);
            return;
        }

        const startDate = new Date(startAt);
        const endDate = new Date(endAt);
        if (!startDate.getTime() || !endDate.getTime()) {
            lastAvailability = null;
            if (serviceOptionInput) serviceOptionInput.value = "";
            setAvailabilityState("กรุณาเลือกวันและเวลาให้ถูกต้อง", "warning");
            return;
        }

        if (endDate <= startDate) {
            lastAvailability = null;
            if (serviceOptionInput) serviceOptionInput.value = "";
            setAvailabilityState("เวลาสิ้นสุดต้องมากกว่าเวลาเริ่มต้น", "warning");
            return;
        }

        setAvailabilityState("กำลังตรวจสอบรถว่าง...", "info");

        try {
            const baseUrl = window.appBasePath || "/";
            const url = `${baseUrl}booking/availability?vehicleType=${encodeURIComponent(vehicleType)}&startAt=${encodeURIComponent(startAt)}&endAt=${encodeURIComponent(endAt)}&bookingMode=${encodeURIComponent(bookingMode)}`;
            const response = await fetch(url, { headers: { "Accept": "application/json" } });
            if (!response.ok) {
                lastAvailability = null;
                if (serviceOptionInput) serviceOptionInput.value = "";
                setAvailabilityState("ไม่สามารถตรวจสอบรถว่างได้ในขณะนี้", "warning");
                return;
            }

            const result = await response.json();
            if (!result?.ok) {
                lastAvailability = null;
                if (serviceOptionInput) serviceOptionInput.value = "";
                const message = result?.message || "กรุณาตรวจสอบข้อมูลที่กรอก";
                setAvailabilityState(message, "warning");
                return;
            }

            lastAvailability = result;

            if (result.total === 0) {
                setAvailabilityState(result.message || "ไม่พบรถบริษัทในประเภทรถนี้", "warning");
                return;
            }

            if (result.available > 0) {
                if (serviceOptionInput) serviceOptionInput.value = "";
                const vehicleLabel = isElectricVehicle(vehicleType) ? "รถไฟฟ้า" : "รถ";
                setAvailabilityState(`มี${vehicleLabel}ว่าง ${result.available} จาก ${result.total} คันในช่วงเวลาที่เลือก`, "success");
                return;
            }

            if (isElectricVehicle(vehicleType)) {
                setAvailabilityState("ไม่มีรถไฟฟ้าว่างในช่วงเวลานี้", "danger");
                return;
            }

            if (isSpecialOccasionSelected() && serviceOptionInput) serviceOptionInput.value = "external";
            setAvailabilityState("ไม่มีรถว่างในช่วงเวลานี้ ระบบจะส่งคำขอไปผู้ให้บริการภายนอก", "danger");
        } catch {
            lastAvailability = null;
            if (serviceOptionInput) serviceOptionInput.value = "";
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

        // require outTrip only when out
        if (outTrip) {
            if (m === "out") {
                outTrip.setAttribute("required", "required");
                outTrip.disabled = false;
            } else {
                outTrip.removeAttribute("required");
                outTrip.value = "";
                outTrip.disabled = true;
            }
        }

        // Special occasion fields availability will be controlled by vehicle type (van only)
        // (Handled by updateSpecialOccasionUI)

        updateAvailability();
        updateElectricModeUI();

        // NOTE: ถ้าอนาคตอยาก disable fields เมื่อ personal ให้ทำตรงนี้ได้
        // const disable = (m === "personal");
        // if (form) $$("input,select,textarea", form).forEach(el => { ... });
    }

    function updateElectricModeUI() {
        if (!approvalPreview) return;

        const isOutProvince = (modeHidden?.value || "in").toLowerCase() === "out";
        approvalPreview.style.display = isOutProvince && isElectricVehicle(getSelectedVehicleType()) ? "none" : "";
    }

    async function updateApprovalPreview() {
        if (!approvalPreview) return;

        try {
            const baseUrl = window.appBasePath || "/";
            const params = new URLSearchParams();
            params.set('bookingMode', (modeHidden?.value || 'in').toLowerCase());
            params.set('vehicleType', getSelectedVehicleType());
            const special = specialOccasionType ? (specialOccasionType.value || '') : '';
            params.set('specialOccasionType', special);

            const url = `${baseUrl}booking/preview-approvals?${params.toString()}`;
            const resp = await fetch(url, { headers: { Accept: 'application/json' } });
            if (!resp.ok) return;
            const data = await resp.json();
            if (!data?.ok) {
                // show note
                approvalPreview.innerHTML = `<div class="fw-semibold mb-3">ขั้นตอนการอนุมัติ</div><div class="text-muted small">${(data?.message) ? data.message : 'ไม่พบข้อมูลสายอนุมัติ'}</div>`;
                return;
            }

            const approvers = data.approvers || [];
            let html = '<div class="fw-semibold mb-3">ขั้นตอนการอนุมัติ</div>';
            if (approvers.length === 0) {
                html += `<div class="text-muted small">เงื่อนไขการอนุมัติขึ้นอยู่กับประเภทคำขอและสายบังคับบัญชา</div>`;
            } else {
                html += '<div class="vb-steps">';
                approvers.forEach(a => {
                    const pos = a.position || '';
                    const name = a.name || '';
                    const level = a.levelNo || '';
                    html += `
                        <div class="vb-step">
                            <div class="vb-step-badge">${level}</div>
                            <div class="vb-step-body">
                                <div class="vb-step-title">${pos}</div>
                                <div class="vb-approver-card">
                                    <div class="vb-approver-row">
                                        <span class="vb-approver-label">ชื่อ-นามสกุล</span>
                                        <span class="vb-approver-value">${name}</span>
                                    </div>
                                </div>
                            </div>
                        </div>`;
                });
                html += '</div>';
            }

            approvalPreview.innerHTML = html;
        } catch (e) {
            // ignore errors silently
        }
    }

    function updateSpecialOccasionUI() {
        const vehicleType = getSelectedVehicleType().toLowerCase();
        const allow = vehicleType === "van";

        if (specialOccasionType) {
            specialOccasionType.disabled = !allow;
            if (!allow) specialOccasionType.value = "";
        }
        if (specialOccasionRemark) {
            specialOccasionRemark.disabled = !allow;
            if (!allow) specialOccasionRemark.value = "";
        }
        // show/hide the special occasion columns entirely
        const cols = $$('.vb-special-occasion-col');
        cols.forEach(c => {
            c.style.display = allow ? "" : "none";
        });
    }

    function shouldAskServiceChoice() {
        const mode = (modeHidden?.value || "in").toLowerCase();
        if (mode !== "out") return false;
        if (isElectricVehicle(getSelectedVehicleType())) return false;
        if (!lastAvailability?.ok) return false;
        if (lastAvailability.total === 0) return true;
        return lastAvailability.available === 0;
    }

    function showServiceChoiceModal() {
        if (!choiceModalEl) return false;
        const modal = bootstrap?.Modal ? bootstrap.Modal.getOrCreateInstance(choiceModalEl) : null;
        if (!modal) return false;
        modal.show();
        return true;
    }

    if (form) {
        form.addEventListener("submit", (e) => {
            if (isSubmitting) return;

            const vehicleType = getSelectedVehicleType();

            if (isElectricVehicle(vehicleType) && (!lastAvailability?.ok || lastAvailability.available <= 0)) {
                e.preventDefault();
                setAvailabilityState("ไม่มีรถไฟฟ้าว่างในช่วงเวลานี้", "danger");
                return;
            }

            const currentChoice = (serviceOptionInput?.value || "").toLowerCase();
            if (shouldAskServiceChoice()) {
                if (isSpecialOccasionSelected()) {
                    if (serviceOptionInput) serviceOptionInput.value = "external";
                } else if (currentChoice !== "personal" && currentChoice !== "external") {
                    e.preventDefault();
                    showServiceChoiceModal();
                }
            }
        });
    }

    choiceButtons.forEach(btn => {
        btn.addEventListener("click", () => {
            const choice = (btn.getAttribute("data-vb-choice") || "").toLowerCase();
            if (!choice) return;

            if (serviceOptionInput) serviceOptionInput.value = choice;

            const modal = choiceModalEl && bootstrap?.Modal ? bootstrap.Modal.getOrCreateInstance(choiceModalEl) : null;
            if (modal) modal.hide();

            if (form) {
                isSubmitting = true;
                form.requestSubmit();
            }
        });
    });

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
            if (serviceOptionInput) serviceOptionInput.value = "";
            lastAvailability = null;
            setAvailabilityState("", null);

            // ถ้าใช้ custom UI ที่ไม่ใช่ native reset เช่น select2 ต้อง reset เพิ่มเองตรงนี้
        });
    }

    vehicleTypeInputs.forEach(el => el.addEventListener("change", updateAvailability));
    vehicleTypeInputs.forEach(el => el.addEventListener("change", updateElectricModeUI));
    vehicleTypeInputs.forEach(el => el.addEventListener("change", updateSpecialOccasionUI));
    vehicleTypeInputs.forEach(el => el.addEventListener("change", updateApprovalPreview));
    if (startAtInput) startAtInput.addEventListener("change", updateAvailability);
    if (endAtInput) endAtInput.addEventListener("change", updateAvailability);
    if (specialOccasionType) specialOccasionType.addEventListener("change", updateApprovalPreview);

    // ===== init =====
    // ถ้ามีค่าเดิมใน hidden ให้ยึดค่านั้น, ไม่งั้น default "in"
    setBookingMode(modeHidden?.value || "in");
    updateAvailability();
    updateElectricModeUI();
    updateSpecialOccasionUI();
    updateApprovalPreview();
})();
