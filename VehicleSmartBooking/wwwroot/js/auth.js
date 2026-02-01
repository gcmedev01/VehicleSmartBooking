(function () {
    const shell = document.getElementById('authShell');
    if (!shell) return;

    // Toggle local username/password form (ยังอยู่หน้าเดิม)
    const toggleLocal = document.getElementById('toggleLocal');
    const localForm = document.getElementById('localForm');
    const localChevron = document.getElementById('localChevron');

    if (toggleLocal && localForm && localChevron) {
        toggleLocal.addEventListener('click', () => {
            localForm.classList.toggle('is-open');
            localChevron.textContent = localForm.classList.contains('is-open') ? '⌄' : '›';
        });
    }

    // Switch signup mode
    const goSignup = document.getElementById('goSignup');
    const backToLogin = document.getElementById('backToLogin');
    const overlayBtn = document.getElementById('overlayBtn');

    function setSignupMode(isSignup) {
        shell.classList.toggle('is-signup', isSignup);

        // เปลี่ยนข้อความ overlay ให้เหมือนตัวอย่าง (optional)
        const title = document.getElementById('overlayTitle');
        const text = document.getElementById('overlayText');

        if (title) title.textContent = isSignup ? 'Welcome Back!' : 'Hello, Friend!';
        if (text) {
            text.textContent = isSignup
                ? 'Enter your personal details to use all of site features'
                : 'Register with your personal details to use all of site features';
        }
        if (overlayBtn) overlayBtn.textContent = isSignup ? 'SIGN IN' : 'SIGN UP';
    }

    if (goSignup) goSignup.addEventListener('click', () => setSignupMode(true));
    if (backToLogin) backToLogin.addEventListener('click', () => setSignupMode(false));
    if (overlayBtn) overlayBtn.addEventListener('click', () => setSignupMode(!shell.classList.contains('is-signup')));
})();
