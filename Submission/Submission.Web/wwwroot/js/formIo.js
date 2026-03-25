function renderForm(layOutURLOrString, formData, submitFuction, ReadOnly = false, divId = "FormId", redirectOverrideUrl, disabledFields = false) {
    renderForm.draftSubmissionData = "";
    var formReference

    if (formData != "" && formData != null && Object.prototype.toString.call(formData) === "[object String]") {
        formData = JSON.parse(formData);
    }
    if (ReadOnly == 'True') {
        layOut = layOutURLOrString;
        if (layOutURLOrString.startsWith("http") == false) {
            layOut = JSON.parse(layOutURLOrString);
        }

        Formio.createForm(document.getElementById(divId), layOut, { viewAsHtml: true, readOnly: true }).then(function (form) {
            form.submission = {
                data: formData
            };
            formReference = form;
        });
    } else {
        layOut = layOutURLOrString;
        if (layOutURLOrString.startsWith("http") == false) {
            layOut = JSON.parse(layOutURLOrString);
        }
        Formio.createForm(document.getElementById(divId), layOut, { "disabled": { "Name": disabledFields } }).then(function (form) {
            form.submission = {
                data: formData
            };
            form.on('change', function () { // Saves current data in form for saving drafts / external submits
                renderForm.draftSubmissionData = JSON.stringify(form.submission.data);
            });
            formReference = form;
        });
    }

    function showError(message) {
        var errorDiv = document.getElementById('errorMsg');
        if (errorDiv) {
            errorDiv.innerHTML = message;
            errorDiv.style.display = 'block';
        }
    }
 
    function hideError() {
        var errorDiv = document.getElementById('errorMsg');
        if (errorDiv) {
            errorDiv.style.display = 'none';
        }
    }

    function AddListenerTo() {
        externalSubmit.addEventListener("click", function SubmitExternal() {
            externalSubmit.removeEventListener("click", SubmitExternal);
            hideError(); 
            var Response = submitForm();
            if (Response == "BAD") {
                AddListenerTo();
            }
        });
    }

    var externalSubmit = document.getElementById("ExternalSubmit");
    if (externalSubmit != null && externalSubmit.getAttribute("click") !== "true") {
        AddListenerTo();
    }

    function submitForm() {
        if (formReference.checkValidity(null, true, null, false)) {
            Data = renderForm.draftSubmissionData;
            Formio.fetch(submitFuction,{
                body: JSON.stringify(Data),
                headers: { 'content-type': 'application/json' },
                method: 'POST',
                mode: 'cors'
            }).then(function (response) {
                if (response.ok) {
                    hideError();
                    setTimeout(function () { window.location.href = redirectOverrideUrl; }, 1);
                    return "GOOD";
                } else {
                    response.text().then(function (errorText) {
                        let message = errorText;
                        try {
                            
                            const obj = JSON.parse(errorText);
                            if (obj && obj.message) {
                                message = obj.message;
                            }
                        } catch (e) {
                           
                        }
                        showError("An error occurred while saving");
                    });
                    return "BAD";
                }
            }).catch(function (error) {
                showError("Network error occurred. Please try again.");
                return "BAD";
            });
        } else {
            showError("Please fill in all required fields correctly.");
            return "BAD";
        }
    }
};