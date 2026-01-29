let dmnTable = null;
let dataTable = null;

// Initialize on page load
$(document).ready(function () {
    console.log('DMN Manager initialized');

    loadDmnTable();

    // Event Listeners
    $('#addRuleBtn').on('click', showAddRuleModal);
    $('#refreshBtn').on('click', loadDmnTable);
    $('#validateBtn').on('click', validateDmn);
    $('#deployBtn').on('click', deployDmn);
    $('#saveRuleBtn').on('click', saveRule);
    $('#confirmDeleteBtn').on('click', deleteRule);
});

/**
 * Load the complete DMN table from TRE-UI Controller
 */
function loadDmnTable() {
    showLoading(true);

    $.ajax({
        url: '/Dmn/GetTable',
        method: 'GET',
        success: function (data) {
            dmnTable = data;
            displayDmnInfo(data);
            buildTableHeaders(data);
            displayRules(data);
            showLoading(false);
            console.log('[OK] DMN table loaded successfully');
        },
        error: function (xhr, status, error) {
            showLoading(false);
            console.error('[ERROR] Error loading DMN table:', {
                status: xhr.status,
                statusText: xhr.statusText,
                responseText: xhr.responseText,
                error: error
            });

            let errorMessage = 'Error loading DMN table';
            if (xhr.status === 401) {
                errorMessage += ': Unauthorized - Your session may have expired. Please refresh the page.';
            } else if (xhr.status === 403) {
                errorMessage += ': Forbidden - You do not have permission to access this resource.';
            } else {
                errorMessage += ': ' + (xhr.responseJSON?.message || xhr.statusText || error);
            }

            showAlert(errorMessage, 'danger');
        }
    });
}

/**
 * Display DMN table information
 */
function displayDmnInfo(table) {
    $('#decisionId').text(table.decisionId);
    $('#decisionName').text(table.decisionName);
    $('#hitPolicy').text(table.hitPolicy);
    $('#dmnInfo').show();
}

/**
 * Build dynamic table headers based on inputs and outputs (excluding submissionId)
 */
function buildTableHeaders(table) {
    const headerRow = $('#rulesTable thead tr');

    // Remove existing dynamic columns
    headerRow.find('th:not(:first):not(:nth-child(2)):not(:last)').remove();

    // Add input columns (excluding submissionId)
    let visibleInputCount = 0;
    table.inputs.forEach(input => {
        const label = input.label || input.expression || 'Input';
        // Skip submissionId input
        if (label.toLowerCase() === 'submissionid') {
            return;
        }
        headerRow.find('th:nth-child(2)').after(`<th class="text-dark bg-light">Input: ${label}</th>`);
        visibleInputCount++;
    });

    // Add output columns (after inputs and description)
    const lastInputIndex = 2 + visibleInputCount;
    table.outputs.forEach(output => {
        const label = output.label || output.name || 'Output';
        headerRow.find(`th:nth-child(${lastInputIndex})`).after(`<th class="text-dark bg-light">Output: ${label}</th>`);
    });
}

/**
 * Display rules in the DataTable (excluding submissionId column)
 */
function displayRules(table) {
    console.log('[INFO] displayRules called with', table.rules.length, 'rules');

    if (dataTable) {
        console.log('[INFO] Destroying existing DataTable');
        dataTable.destroy();
        dataTable = null;
    }

    const tbody = $('#rulesTable tbody');
    tbody.empty();
    console.log('[INFO] Cleared table body, adding', table.rules.length, 'rows');

    table.rules.forEach((rule, index) => {
        const row = $('<tr>');

        // Rule number
        row.append(`<td>${index + 1}</td>`);

        // Description
        row.append(`<td>${rule.description || '-'}</td>`);

        // Input entries (excluding submissionId)
        rule.inputEntries.forEach((entry, inputIndex) => {
            const input = table.inputs[inputIndex];
            const label = input.label || input.expression || 'Input';

            // Skip submissionId input
            if (label.toLowerCase() === 'submissionid') {
                return;
            }

            const displayText = entry.text === '-' ? '<em>Any</em>' :
                entry.text === '' ? '<em>Empty</em>' :
                    entry.text;
            row.append(`<td>${displayText}</td>`);
        });

        // Output entries
        rule.outputEntries.forEach(entry => {
            const displayText = entry.text || '<em>Empty</em>';
            row.append(`<td><code>${displayText}</code></td>`);
        });

        // Actions
        const actions = `
            <button class="btn btn-sm btn-outline-primary edit-rule" data-rule-id="${rule.id}" title="Edit Rule">
                <i class="fas fa-edit"></i>
            </button>
            <button class="btn btn-sm btn-outline-danger delete-rule" data-rule-id="${rule.id}" title="Delete Rule">
                <i class="fas fa-trash"></i>
            </button>
        `;
        row.append(`<td class="text-nowrap">${actions}</td>`);

        tbody.append(row);
    });

    console.log('[INFO] Added', table.rules.length, 'rows to table body');

    console.log('[INFO] Initializing DataTable...');
    dataTable = $('#rulesTable').DataTable({
        pageLength: 25,
        order: [[0, 'asc']],
        responsive: true,
        language: {
            emptyTable: "No rules found in the DMN table"
        }
    });

    console.log('[INFO] DataTable initialized with', dataTable.rows().count(), 'rows');
    $('#rulesTable').show();

    $('#rulesTable').off('click', '.edit-rule').on('click', '.edit-rule', function () {
        const ruleId = $(this).data('rule-id');
        showEditRuleModal(ruleId);
    });

    $('#rulesTable').off('click', '.delete-rule').on('click', '.delete-rule', function () {
        const ruleId = $(this).data('rule-id');
        showDeleteModal(ruleId);
    });
}

/**
 * Show modal for adding a new rule (submissionId hidden and defaults to "-" for Any)
 */
function showAddRuleModal() {
    if (!dmnTable) {
        showAlert('Please wait for DMN table to load', 'warning');
        return;
    }

    $('#ruleModalLabel').text('Add New Rule');
    $('#ruleId').val('');
    $('#isEdit').val('false');
    $('#ruleDescription').val('');

    // Build input fields (hide submissionId and set default to "-")
    const inputContainer = $('#inputFieldsContainer');
    inputContainer.empty();
    dmnTable.inputs.forEach((input, index) => {
        const label = input.label || input.expression || `Input ${index + 1}`;

        // Hide submissionId input and store default value
        if (label.toLowerCase() === 'submissionid') {
            const hiddenInput = $('<input>')
                .attr('type', 'hidden')
                .addClass('input-value')
                .attr('data-index', index)
                .val('-');
            inputContainer.append(hiddenInput);
            return;
        }

        const inputDiv = $('<div>').addClass('mb-2');
        const labelElem = $('<label>').addClass('form-label').text(label);
        const inputElem = $('<input>')
            .attr('type', 'text')
            .addClass('form-control input-value')
            .attr('data-index', index)
            .attr('placeholder', "Enter value (use '-' for any, leave empty for none)");
        const helpText = $('<small>')
            .addClass('form-text text-muted')
            .text("Tip: Use '-' to match any value, leave empty for no value");

        inputDiv.append(labelElem, inputElem, helpText);
        inputContainer.append(inputDiv);
    });

    // Build output fields
    const outputContainer = $('#outputFieldsContainer');
    outputContainer.empty();
    dmnTable.outputs.forEach((output, index) => {
        const label = output.label || output.name || `Output ${index + 1}`;

        const outputDiv = $('<div>').addClass('mb-2');
        const labelElem = $('<label>').addClass('form-label').text(label);
        const inputElem = $('<input>')
            .attr('type', 'text')
            .addClass('form-control output-value')
            .attr('data-index', index)
            .attr('placeholder', 'Enter value (e.g., "stringValue" or expression)');
        const helpText = $('<small>')
            .addClass('form-text text-muted')
            .text('Tip: Use quotes for string literals, expressions for calculations');

        outputDiv.append(labelElem, inputElem, helpText);
        outputContainer.append(outputDiv);
    });

    $('#ruleModal').modal('show');
}

/**
 * Show modal for editing an existing rule (submissionId hidden)
 */
function showEditRuleModal(ruleId) {
    const rule = dmnTable.rules.find(r => r.id === ruleId);
    if (!rule) {
        showAlert('Rule not found', 'danger');
        return;
    }

    $('#ruleModalLabel').text('Edit Rule');
    $('#ruleId').val(rule.id);
    $('#isEdit').val('true');
    $('#ruleDescription').val(rule.description || '');

    // Build input fields with existing values (hide submissionId)
    const inputContainer = $('#inputFieldsContainer');
    inputContainer.empty();
    const inputValues = []; // Store values to set after modal is shown
    dmnTable.inputs.forEach((input, index) => {
        const label = input.label || input.expression || `Input ${index + 1}`;
        const value = rule.inputEntries[index]?.text || '';

        // Hide submissionId input but keep its value
        if (label.toLowerCase() === 'submissionid') {
            const hiddenInput = $('<input>')
                .attr('type', 'hidden')
                .addClass('input-value')
                .attr('data-index', index)
                .val(value);
            inputContainer.append(hiddenInput);
            return;
        }

        const inputDiv = $('<div>').addClass('mb-2');
        const labelElem = $('<label>').addClass('form-label').text(label);
        const inputElem = $('<input>')
            .attr('type', 'text')
            .addClass('form-control input-value')
            .attr('data-index', index)
            .attr('placeholder', "Enter value (use '-' for any, leave empty for none)");
        const helpText = $('<small>')
            .addClass('form-text text-muted')
            .text("Tip: Use '-' to match any value, leave empty for no value");

        inputDiv.append(labelElem, inputElem, helpText);
        inputContainer.append(inputDiv);

        // Store the element and value to set after modal is shown
        inputValues.push({ element: inputElem, value: value });
    });

    // Build output fields with existing values
    const outputContainer = $('#outputFieldsContainer');
    outputContainer.empty();
    const outputValues = []; // Store values to set after modal is shown
    dmnTable.outputs.forEach((output, index) => {
        const label = output.label || output.name || `Output ${index + 1}`;
        const value = rule.outputEntries[index]?.text || '';

        const outputDiv = $('<div>').addClass('mb-2');
        const labelElem = $('<label>').addClass('form-label').text(label);
        const inputElem = $('<input>')
            .attr('type', 'text')
            .addClass('form-control output-value')
            .attr('data-index', index)
            .attr('placeholder', 'Enter value (e.g., "stringValue" or expression)');
        const helpText = $('<small>')
            .addClass('form-text text-muted')
            .text('Tip: Use quotes for string literals, expressions for calculations');

        outputDiv.append(labelElem, inputElem, helpText);
        outputContainer.append(outputDiv);

        // Store the element and value to set after modal is shown
        outputValues.push({ element: inputElem, value: value });
    });

    // Show the modal first, then set values after it's fully displayed
    $('#ruleModal').off('shown.bs.modal').one('shown.bs.modal', function () {
        // Set input values
        inputValues.forEach((item) => {
            item.element.val(item.value);
        });

        // Set output values
        outputValues.forEach((item) => {
            item.element.val(item.value);
        });
    });

    $('#ruleModal').modal('show');
}

/**
 * Save rule (add or update)
 */
function saveRule() {
    const isEdit = $('#isEdit').val() === 'true';
    const ruleId = $('#ruleId').val();
    const description = $('#ruleDescription').val();

    // Collect input values (includes hidden submissionId)
    const inputValues = [];
    $('.input-value').each(function () {
        inputValues.push($(this).val());
    });

    // Collect output values
    const outputValues = [];
    $('.output-value').each(function () {
        outputValues.push($(this).val());
    });

    // Validate
    if (inputValues.length !== dmnTable.inputs.length) {
        showAlert('Invalid number of input values', 'danger');
        return;
    }

    if (outputValues.length !== dmnTable.outputs.length) {
        showAlert('Invalid number of output values', 'danger');
        return;
    }

    const requestData = isEdit ? {
        ruleId: ruleId,
        description: description,
        inputValues: inputValues,
        outputValues: outputValues
    } : {
        description: description,
        inputValues: inputValues,
        outputValues: outputValues
    };

    const url = isEdit ? '/Dmn/UpdateRule' : '/Dmn/AddRule';
    const method = isEdit ? 'PUT' : 'POST';

    $.ajax({
        url: url,
        method: method,
        contentType: 'application/json',
        data: JSON.stringify(requestData),
        success: function (response) {
            console.log('[OK] Save rule response:', response);
            $('#ruleModal').modal('hide');
            showAlert(response.message, response.success ? 'success' : 'danger');

            if (response.success) {
                setTimeout(function () {
                    console.log('[INFO] Reloading table after save...');
                    loadDmnTable();
                }, 300);
            }
        },
        error: function (xhr, status, error) {
            showAlert('Error saving rule: ' + (xhr.responseJSON?.message || error), 'danger');
            console.error('Error saving rule:', xhr);
        }
    });
}

/**
 * Show delete confirmation modal
 */
function showDeleteModal(ruleId) {
    $('#deleteRuleId').val(ruleId);
    $('#deleteModal').modal('show');
}

/**
 * Delete a rule
 */
function deleteRule() {
    const ruleId = $('#deleteRuleId').val();

    $.ajax({
        url: '/Dmn/DeleteRule/' + encodeURIComponent(ruleId),
        method: 'DELETE',
        success: function (response) {
            console.log('[OK] Delete rule response:', response);
            $('#deleteModal').modal('hide');
            showAlert(response.message, response.success ? 'success' : 'danger');

            if (response.success) {
                setTimeout(function () {
                    console.log('[INFO] Reloading table after delete...');
                    loadDmnTable();
                }, 300);
            }
        },
        error: function (xhr, status, error) {
            console.error('[ERROR] Delete rule failed:', xhr);
            $('#deleteModal').modal('hide');
            showAlert('Error deleting rule: ' + (xhr.responseJSON?.message || error), 'danger');
        }
    });
}

/**
 * Validate DMN
 */
function validateDmn() {
    $.ajax({
        url: '/Dmn/ValidateDmn',
        method: 'GET',
        success: function (response) {
            showAlert(response.message, response.success ? 'success' : 'warning');
        },
        error: function (xhr, status, error) {
            showAlert('Validation failed: ' + (xhr.responseJSON?.message || error), 'danger');
            console.error('Validation error:', xhr);
        }
    });
}

/**
 * Deploy DMN to Zeebe
 */
function deployDmn() {
    if (!confirm('Are you sure you want to deploy the DMN?')) {
        return;
    }

    $.ajax({
        url: '/Dmn/DeployDmn',
        method: 'POST',
        success: function (response) {
            showAlert(response.message, response.success ? 'success' : 'danger');
        },
        error: function (xhr, status, error) {
            showAlert('Deployment failed: ' + (xhr.responseJSON?.message || error), 'danger');
            console.error('Deployment error:', xhr);
        }
    });
}

/**
 * Show/hide loading spinner
 */
function showLoading(show) {
    if (show) {
        $('#loadingSpinner').show();
        $('#rulesTable').hide();
    } else {
        $('#loadingSpinner').hide();
    }
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
    $('#alertContainer').html(alertHtml);

    // Auto-dismiss after 5 seconds
    setTimeout(() => {
        $('.alert').alert('close');
    }, 5000);
}

/**
 * Hide alert
 */
function hideAlert() {
    $('#alertContainer').empty();
}