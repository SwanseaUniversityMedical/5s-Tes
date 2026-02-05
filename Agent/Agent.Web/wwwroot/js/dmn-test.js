// DMN Test JavaScript - Refactored to use TRE-UI Controller

// Initialize on page load
$(document).ready(function () {
    // Show initial message
    $('#noResults').show();

    // Event Listeners
    $('#evaluateBtn').on('click', evaluateDmn);
    $('#clearBtn').on('click', clearInputs);

    // Quick test case buttons
    $('.test-case').on('click', function () {
        const project = $(this).data('project');
        const user = $(this).data('user');
        const submission = $(this).data('submission');

        $('#projectInput').val(project);
        $('#userInput').val(user);
        $('#submissionIdInput').val(submission);

        // Auto-evaluate
        evaluateDmn();
    });
});

/**
 * Evaluate DMN with current input values
 */
function evaluateDmn() {
    hideAlert();
    $('#evaluatingSpinner').show();
    $('#resultsContainer').hide();
    $('#noResults').hide();

    // Collect input variables
    const inputVariables = {
        project: $('#projectInput').val() || '',
        user: $('#userInput').val() || '',
        submissionId: $('#submissionIdInput').val() || ''
    };

    $.ajax({
        url: '/Dmn/TestDmn',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({ inputVariables: inputVariables }),
        success: function (response) {
            $('#evaluatingSpinner').hide();
            displayResults(response, inputVariables);
        },
        error: function (xhr, status, error) {
            $('#evaluatingSpinner').hide();
            $('#noResults').hide();
            showAlert('Error evaluating DMN: ' + (xhr.responseJSON?.message || error), 'danger');
            console.error('Error evaluating DMN:', xhr);
        }
    });
}

/**
 * Display evaluation results
 */
function displayResults(response, inputVariables) {
    if (!response.success) {
        showAlert('Evaluation failed: ' + response.message, 'danger');
        return;
    }

    const resultsContent = $('#resultsContent');
    resultsContent.empty();

    // Show input values used
    resultsContent.append(`
        <div class="card mb-3 border-info">
            <div class="card-header bg-info text-white">
                <strong>Input Values Used:</strong>
            </div>
            <div class="card-body">
                <dl class="row mb-0">
                    <dt class="col-sm-4">Project:</dt>
                    <dd class="col-sm-8"><code>${inputVariables.project || '(empty)'}</code></dd>
                    <dt class="col-sm-4">User:</dt>
                    <dd class="col-sm-8"><code>${inputVariables.user || '(empty)'}</code></dd>
                    <dt class="col-sm-4">Submission ID:</dt>
                    <dd class="col-sm-8"><code>${inputVariables.submissionId || '(empty)'}</code></dd>
                </dl>
            </div>
        </div>
    `);

    // Show matched rules
    if (response.matchedRules && response.matchedRules.length > 0) {
        resultsContent.append(`
            <div class="card border-success">
                <div class="card-header bg-success text-white">
                    <strong>Matched Rules (${response.matchedRules.length}):</strong>
                </div>
                <div class="card-body">
                    <div id="rulesAccordion"></div>
                </div>
            </div>
        `);

        const accordion = $('#rulesAccordion');

        response.matchedRules.forEach((ruleResult, index) => {
            // Parse the rule result
            const credentials = parseCredentialsOutput(ruleResult);

            const accordionItem = `
                <div class="accordion-item">
                    <h2 class="accordion-header" id="heading${index}">
                        <button class="accordion-button ${index > 0 ? 'collapsed' : ''}" type="button"
                                data-bs-toggle="collapse" data-bs-target="#collapse${index}"
                                aria-expanded="${index === 0 ? 'true' : 'false'}" aria-controls="collapse${index}">
                            Result Set ${index + 1} (${credentials.length} credentials)
                        </button>
                    </h2>
                    <div id="collapse${index}" class="accordion-collapse collapse ${index === 0 ? 'show' : ''}"
                         aria-labelledby="heading${index}" data-bs-parent="#rulesAccordion">
                        <div class="accordion-body">
                            ${formatCredentials(credentials)}
                        </div>
                    </div>
                </div>
            `;
            accordion.append(accordionItem);
        });
    } else {
        resultsContent.append(`
            <div class="alert alert-warning">
                <i class="fas fa-exclamation-triangle"></i> No rules matched the input criteria.
            </div>
        `);
    }

    showAlert('DMN evaluation completed successfully', 'success');
    $('#resultsContainer').show();
}

/**
 * Parse credentials output from DMN result
 */
function parseCredentialsOutput(ruleResult) {
    const credentials = [];

    // The result structure depends on your DMN output
    // Adjust based on actual response format
    if (typeof ruleResult === 'object') {
        for (const key in ruleResult) {
            if (ruleResult.hasOwnProperty(key)) {
                credentials.push({
                    key: key,
                    value: ruleResult[key]
                });
            }
        }
    }

    return credentials;
}

/**
 * Format credentials for display
 */
function formatCredentials(credentials) {
    if (credentials.length === 0) {
        return '<p class="text-muted">No credentials returned</p>';
    }

    let html = '<table class="table table-sm table-bordered">';
    html += '<thead class="table-light"><tr><th>Credential Key</th><th>Value</th></tr></thead>';
    html += '<tbody>';

    credentials.forEach(cred => {
        const displayValue = typeof cred.value === 'string' && cred.value.length > 100
            ? cred.value.substring(0, 100) + '...'
            : JSON.stringify(cred.value);

        html += `
            <tr>
                <td><strong>${escapeHtml(cred.key)}</strong></td>
                <td><code>${escapeHtml(displayValue)}</code></td>
            </tr>
        `;
    });

    html += '</tbody></table>';
    return html;
}

/**
 * Clear all input fields
 */
function clearInputs() {
    $('#projectInput').val('');
    $('#userInput').val('');
    $('#submissionIdInput').val('');
    $('#resultsContainer').hide();
    $('#noResults').show();
    hideAlert();
}

/**
 * Show alert message
 */
function showAlert(message, type) {
    const alertHtml = `
        <div class="alert alert-${type} alert-dismissible fade show" role="alert">
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        </div>
    `;
    $('#resultAlert').html(alertHtml);

    // Auto-dismiss after 5 seconds
    setTimeout(() => {
        $('.alert').alert('close');
    }, 5000);
}

/**
 * Hide alert
 */
function hideAlert() {
    $('#resultAlert').empty();
}

/**
 * Escape HTML to prevent XSS
 */
function escapeHtml(text) {
    const map = {
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#039;'
    };
    return String(text).replace(/[&<>"']/g, function (m) { return map[m]; });
}