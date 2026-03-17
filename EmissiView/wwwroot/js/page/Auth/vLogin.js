document.addEventListener('DOMContentLoaded', function () {
    const form = document.getElementById('loginForm');
    if (!form) return;

    form.addEventListener('submit', Login);
});

async function Login(event) {
    showLoader();
    removeError();
    event.preventDefault();

    try {
        const form = event.target;
        const submitButton = event.submitter;
        const formData = new FormData(form);

        if (submitButton && submitButton.name) {
            formData.append(submitButton.name, submitButton.value);
        }

        const response = await fetch(`${basePath}/Auth/Login`, {
            method: 'POST',
            body: formData
        });

        const { success, text = "", errors = [] } = await response.json();

        if (!success) {
            showError(errors);
            hideLoader();
            SwalNG(errors, text);
        } else {
            hideLoader();
            window.location.pathname = `${basePath}/ElectricChart/Index`;
        }
    } catch ({ message }) {
        hideLoader();
        alert(`Exception: ${message}`);
    }
}