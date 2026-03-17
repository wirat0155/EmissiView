function SwalOK(text) {
    Swal.fire({
        title: "Success",
        text: text,
        icon: "success",
        timer: 2000,
        showConfirmButton: false,
        confirmButtonColor: "#28a745",
        customClass: {
            popup: 'swal-success-popup',
            title: 'swal-success-title'
        },
        showClass: {
            popup: 'animate__animated animate__fadeInDown animate__faster'
        },
        hideClass: {
            popup: 'animate__animated animate__fadeOutUp animate__faster'
        }
    });
}
// Helper to get translation or fallback
function getTranslation(key, defaultText) {
    const currentLang = localStorage.getItem('pa_lang') || 'en';
    if (typeof translations !== 'undefined' && translations[currentLang] && translations[currentLang][key]) {
        return translations[currentLang][key];
    }
    return defaultText || key;
}

function SwalNG(errors, text) {
    let message = "";
    let title = "Error"; // Could also be translated

    if (Array.isArray(errors) && errors.length > 0) {
        // Show all error messages in a clean format
        if (errors.length === 1) {
            // Single error - show it directly
            const err = errors[0];
            const msg = err.errorMessage || err.property || "Unknown error";
            // Translate if it's a key
            message = getTranslation(msg, msg);
        } else {
            // Multiple errors - show as a styled list
            message = '<div style="text-align: left; padding: 0 20px;">';
            errors.forEach((error) => {
                const errorMsg = error.errorMessage || error.property || "Unknown error";
                // Translate key
                const translatedMsg = getTranslation(errorMsg, errorMsg);
                message += `<div style="margin: 8px 0; display: flex; align-items: start;">
                    <span style="color: #dc3545; margin-right: 8px; font-weight: bold;">•</span>
                    <span>${translatedMsg}</span>
                </div>`;
            });
            message += '</div>';
        }
    } else if (text) {
        // Use the text parameter if provided
        message = getTranslation(text, text);
    } else if (typeof errors === 'string') {
        // If errors is a string, use it directly
        message = getTranslation(errors, errors);
    } else {
        // Generic fallback message
        message = getTranslation("generic_error", "An error occurred. Please try again.");
    }

    // Check if Swal is defined
    if (typeof Swal !== 'undefined') {
        Swal.fire({
            title: title,
            html: message,
            icon: "error",
            confirmButtonText: "OK",
            confirmButtonColor: "#dc3545",
            customClass: {
                popup: 'swal-error-popup',
                title: 'swal-error-title',
                htmlContainer: 'swal-error-content'
            },
            showClass: {
                popup: 'animate__animated animate__fadeInDown animate__faster'
            },
            hideClass: {
                popup: 'animate__animated animate__fadeOutUp animate__faster'
            }
        });
    } else {
        alert(message);
    }
}

$('form').on('input', 'input, select, textarea', function () {
    const inputName = $(this).attr('name');
    if (inputName) {
        // Escape special characters for the property name in the error object
        const escapedInputName = inputName.replace(/([.#\[\]\\'"])/g, '\\$&');
        $('.error#' + escapedInputName).text('');
    }
});

function removeError() {
    $(".error").text("");
    const errorMsg = document.getElementById('errorMessage');
    if (errorMsg) errorMsg.classList.add('hidden');
}

function showError(errors) {
    // If errors is an object (map), convert to array format expected by logic below if needed
    // But typical model state errors come as array of objects { property, errorMessage } or map { key: value }
    // Let's handle both

    const escapeSelector = (selector) => selector.replace(/([.#\[\]\\'"])/g, '\\$&');

    if (Array.isArray(errors)) {
        errors.forEach(error => {
            const escapedProperty = escapeSelector(error.property);
            const msg = getTranslation(error.errorMessage, error.errorMessage);
            $(".error#" + escapedProperty).text(msg);
        });

        const topmostElement = errors
            .map(error => {
                const escapedProperty = escapeSelector(error.property);
                return $(`[name="${escapedProperty}"]`)[0];
            })
            .sort((a, b) => {
                const rectA = a ? a.getBoundingClientRect().top : 0;
                const rectB = b ? b.getBoundingClientRect().top : 0;
                return rectA - rectB;
            })[0];

        if (topmostElement) {
            $(topmostElement).focus();
        }

    } else if (typeof errors === 'object') {
        // Handle map format { key: value }
        for (const [key, value] of Object.entries(errors)) {
            const escapedProperty = escapeSelector(key);
            const msg = getTranslation(value, value);
            $(".error#" + escapedProperty).text(msg);
        }
    }
}

function showLoader() {
    const btn = document.querySelector('button[type="submit"]');
    if (btn) {
        btn.dataset.originalContent = btn.innerHTML;
        btn.disabled = true;
        btn.innerHTML = '<div class="loader inline-block align-middle"></div>';
    }
}

function hideLoader() {
    const btn = document.querySelector('button[type="submit"]');
    if (btn && btn.dataset.originalContent) {
        btn.disabled = false;
        btn.innerHTML = btn.dataset.originalContent;
    }
}




function renderErrorSpans() {
    $('input[data-form], select[data-form], textarea[data-form]').each(function () {
        const formName = $(this).attr('data-form');
        if (!$('#' + formName).length) {
            $('<p>', { class: 'error text-red-500', id: formName }).insertAfter(this);
        }
    });
}

const basePath = (window.location.hostname !== 'localhost' && window.location.hostname !== '127.0.0.1') ? "/" + window.location.pathname.split('/')[1] : ''; // Adjust '/myapp' to your subfolder name

$('form').on('keydown', 'input, select, textarea', function (e) {
    if (e.key === 'Enter') {
        e.preventDefault();
    }
});
