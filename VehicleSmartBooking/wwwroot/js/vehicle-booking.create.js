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
    const attachmentsInput = $("#Attachments");
    const attachmentsRequiredMark = $("#attachmentsRequiredMark");

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

        // Attachments are mandatory for out-of-province bookings, optional for in-province. Do not
        // clear an already-chosen file when switching back to "in" — the files may still be relevant.
        if (attachmentsInput) {
            if (m === "out") {
                attachmentsInput.setAttribute("required", "required");
            } else {
                attachmentsInput.removeAttribute("required");
            }
        }
        if (attachmentsRequiredMark) {
            attachmentsRequiredMark.classList.toggle("d-none", m !== "out");
        }

        updateAvailability();
        updateElectricModeUI();
        // Booking mode (in/out province) affects ApprovalScenario just as much as vehicle type
        // does — recalculate here too, not only on vehicle-type change (this was the root cause
        // of a stale approval chain when the user picked vehicle type before booking mode).
        refreshApprovalChain();

        // NOTE: ถ้าอนาคตอยาก disable fields เมื่อ personal ให้ทำตรงนี้ได้
        // const disable = (m === "personal");
        // if (form) $$("input,select,textarea", form).forEach(el => { ... });
    }

    function updateElectricModeUI() {
        if (!approvalPreview) return;

        const isOutProvince = (modeHidden?.value || "in").toLowerCase() === "out";
        approvalPreview.style.display = isOutProvince && isElectricVehicle(getSelectedVehicleType()) ? "none" : "";
    }

    // ── Approval chain: single source of truth for rendering the preview box ──
    function renderApprovalHeader() {
        return '<div class="fw-semibold mb-3">ขั้นตอนการอนุมัติ</div>';
    }

    function renderApprovalMessage(message) {
        if (!approvalPreview) return;
        approvalPreview.innerHTML = renderApprovalHeader() + `<div class="text-muted small">${message}</div>`;
    }

    // Shown only while required fields (trip type / vehicle type) are missing — never once the
    // data needed to calculate a real chain is present.
    function renderApprovalPlaceholder() {
        const hasTripType = !!(modeHidden?.value || "").trim();
        const hasVehicleType = !!getSelectedVehicleType();

        if (!hasTripType && !hasVehicleType) {
            renderApprovalMessage("กรุณาเลือกประเภทการจองและประเภทรถเพื่อดูสายการอนุมัติ");
        } else if (!hasTripType) {
            renderApprovalMessage("กรุณาเลือกประเภทการจอง");
        } else {
            renderApprovalMessage("กรุณาเลือกประเภทรถ");
        }
    }

    function renderApprovalLoading() {
        renderApprovalMessage("กำลังคำนวณสายการอนุมัติ...");
    }

    function renderApprovalResult(data) {
        const approvers = data.approvers || [];

        if (approvers.length === 0) {
            // Data is complete but there is genuinely nothing to show — say so accurately instead
            // of the old generic fallback text (which used to appear even when the request truly
            // has no approval step, e.g. auto-approved, or is auto-routed to an admin).
            if (data.isAdminActionRequired) {
                renderApprovalMessage("คำขอนี้ต้องให้ผู้ดูแลระบบ (Admin) ดำเนินการต่อ ไม่ผ่านสายอนุมัติปกติ");
            } else if (data.isAuto) {
                renderApprovalMessage("คำขอนี้ไม่ต้องผ่านขั้นตอนอนุมัติ (อนุมัติอัตโนมัติ)");
            } else {
                renderApprovalMessage("ไม่พบสายการอนุมัติสำหรับเงื่อนไขนี้");
            }
            return;
        }

        let html = renderApprovalHeader() + '<div class="vb-steps">';
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

        if (approvalPreview) approvalPreview.innerHTML = html;
    }

    // Cancels the previous in-flight request (if any) so a slow/stale response can never overwrite
    // the result for whatever the user has selected by the time it arrives.
    let approvalAbortController = null;

    async function calculateAndRenderApprovalChain() {
        if (!approvalPreview) return;

        const baseUrl = window.appBasePath || "/";
        const params = new URLSearchParams();
        params.set('bookingMode', (modeHidden?.value || 'in').toLowerCase());
        params.set('vehicleType', getSelectedVehicleType());
        params.set('specialOccasionType', specialOccasionType ? (specialOccasionType.value || '') : '');
        params.set('serviceOption', serviceOptionInput ? (serviceOptionInput.value || '') : '');
        params.set('startAt', startAtInput?.value || '');
        params.set('endAt', endAtInput?.value || '');

        if (approvalAbortController) {
            approvalAbortController.abort();
        }
        const controller = new AbortController();
        approvalAbortController = controller;

        renderApprovalLoading();

        try {
            const url = `${baseUrl}booking/preview-approvals?${params.toString()}`;
            const resp = await fetch(url, { headers: { Accept: 'application/json' }, signal: controller.signal });

            // A newer request has already started; let that one own the render.
            if (approvalAbortController !== controller) return;

            if (!resp.ok) {
                renderApprovalMessage('ไม่สามารถคำนวณสายการอนุมัติได้ในขณะนี้');
                return;
            }

            const data = await resp.json();
            if (approvalAbortController !== controller) return;

            if (!data?.ok) {
                renderApprovalMessage(data?.message || 'ไม่พบข้อมูลสายอนุมัติ');
                return;
            }

            renderApprovalResult(data);
        } catch (e) {
            if (e?.name === 'AbortError') return; // superseded by a newer request — ignore
            if (approvalAbortController === controller) {
                renderApprovalMessage('ไม่สามารถคำนวณสายการอนุมัติได้ในขณะนี้');
            }
        } finally {
            if (approvalAbortController === controller) approvalAbortController = null;
        }
    }

    // Central entry point: re-read the current field values fresh from the DOM every time (never
    // rely on values captured at page-load) and either show a placeholder or (re)calculate the
    // real chain. Call this — not calculateAndRenderApprovalChain() directly — from every trigger.
    function refreshApprovalChain() {
        if (!approvalPreview) return;

        const tripType = (modeHidden?.value || "").trim();
        const vehicleType = getSelectedVehicleType();

        if (!tripType || !vehicleType) {
            if (approvalAbortController) {
                approvalAbortController.abort();
                approvalAbortController = null;
            }
            renderApprovalPlaceholder();
            return;
        }

        calculateAndRenderApprovalChain();
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
            refreshApprovalChain();

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

    // Every field that can change ApprovalScenario re-triggers the same central function —
    // no per-field bespoke calculation, matching the single source of truth in ApprovalChainBuilder.
    vehicleTypeInputs.forEach(el => el.addEventListener("change", updateAvailability));
    vehicleTypeInputs.forEach(el => el.addEventListener("change", updateElectricModeUI));
    vehicleTypeInputs.forEach(el => el.addEventListener("change", updateSpecialOccasionUI));
    vehicleTypeInputs.forEach(el => el.addEventListener("change", refreshApprovalChain));
    if (startAtInput) startAtInput.addEventListener("change", updateAvailability);
    if (endAtInput) endAtInput.addEventListener("change", updateAvailability);
    // Multi-day vs single-day affects ApprovalScenario for electric vehicles.
    if (startAtInput) startAtInput.addEventListener("change", refreshApprovalChain);
    if (endAtInput) endAtInput.addEventListener("change", refreshApprovalChain);
    if (specialOccasionType) specialOccasionType.addEventListener("change", refreshApprovalChain);

    // ===== init =====
    // Handles Create (default values), Edit, and returning from a validation error / browser form
    // restore — always re-read the actual current input values rather than assuming page-load state.
    // setBookingMode() already calls refreshApprovalChain() internally, so it doesn't need a
    // separate call here (that would just abort-and-refetch the identical request once, uselessly).
    setBookingMode(modeHidden?.value || "in");
    updateAvailability();
    updateElectricModeUI();
    updateSpecialOccasionUI();
})();
