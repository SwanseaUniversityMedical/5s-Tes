function renderForm(model, layoutUrl, formData, submitFuction, groupName) {
    renderForm.draftSubmissionData = "";

    if (formData != "" && formData != null) {
        formData = JSON.parse(formData);
    }


    Formio.createForm(document.getElementById('FormId'), layoutUrl).then(function (form) {
        form.submission = {
            data: formData
        };
        form.on('change', function () { // Saves current data in form for saving drafts / external submits
            renderForm.draftSubmissionData = JSON.stringify(form.submission.data);
        });
    });

    function AddListenerTo() {
        externalSubmit.addEventListener("click", function SubmitExternal() {

            externalSubmit.removeEventListener("click", SubmitExternal);
            try {
                var Response = submitForm();
            } catch (error) {
                console.error(error)
            }

            if (Response == null || Response == "BAD") {
                AddListenerTo();
            }
        });
    }

    var externalSubmit = document.getElementById("ExternalSubmit");
    if (externalSubmit != null && externalSubmit.getAttribute("click") !== "true") {
        AddListenerTo();
    }

    function submitForm() {
        model.formIoString = renderForm.draftSubmissionData;
        var myId = "#FormId";
        var idd = $(myId).children().attr("id");
        if (this.Formio.forms[idd].checkValidity(null, true, null, false)) {
            Formio.fetch(submitFuction,
                {
                    body: JSON.stringify(model),
                    headers: { 'content-type': 'application/json' },
                    method: 'POST',
                    mode: 'cors'
                }).then(function (response) {
                    if (response.ok) {
                        console.log("Ok");
                        //setTimeout(function () { window.location.href = "/AvailableForms/GetAvailableFormsForGroup/" + groupName; }, 1); //LocalRedirect don't work so we Have to use this to get it working also brakes stuff when it's not delayed by at least a tiny amount

                        return "GOOD"
                    }
                    else {
                        console.log("Bad " + response.message);
                        return "BAD"
                    }
                });
        } else {
            try {
                document.getElementById('errorMsg').textContent = "Submission failed: Please look for validation errors in the form";
                document.getElementById('errorMsg').style.display = "inline-block";
                Custombox.modal.close();
            }
            catch (err) {

            }
            return "BAD"
        }
    }
};

function RenderProjectForm(formUrl, formData, submitUrl, returnUrl = "", model) {
    if (formData != "" && formData != null) {
        formData = JSON.parse(formData);
    }

    Formio.createForm(document.getElementById('FormId'), formUrl).then(function (form) {
        form.nosubmit = true;
        form.submission = {
            data: formData
        };
        form.on('submit', function (submission) {
            return Formio.fetch(submitUrl, {
                body: JSON.stringify(submission),
                headers: { 'content-type': 'application/json' },
                method: 'POST',
                mode: 'cors',
            })
                .then(function (response) {
                    if (response.ok) {
                        console.log("Ok");
                        //redirect here
                        return "GOOD"
                    }
                    else {
                        console.log("Bad " + response.message);
                        alert("error submitting project");
                        return "BAD"
                    }
                })
        });
    });
}