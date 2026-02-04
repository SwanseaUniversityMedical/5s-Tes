"use server";

import { isNextRedirectError } from "@/lib/api/helpers";
import request from "@/lib/api/request";
import { DecisionInfo, AccessRulesData } from "@/types/access-rules";
import { transformRulesToRuleColumns, transformFormDataForApi } from "@/lib/helpers/access-rules-api";

/* ----- API Response Types ------ */

interface DmnRuleSubData {
  id?: string | null;
  text?: string | null;
}

interface DmnRule {
  id?: string | null;
  description?: string | null;
  inputEntries?: Array<DmnRuleSubData> | null;
  outputEntries?: Array<DmnRuleSubData> | null;
}

interface DmnDecisionTable {
  decisionId?: string | null;
  decisionName?: string | null;
  hitPolicy?: string | null;
  inputs?: Array<{
    id?: string | null;
    label?: string | null;
    expression?: string | null;
    typeRef?: string | null;
  }> | null;
  outputs?: Array<{
    id?: string | null;
    label?: string | null;
    name?: string | null;
    typeRef?: string | null;
  }> | null;
  rules?: Array<DmnRule> | null;
}

interface DmnValidateResponse {
  $id?: string;
  success: boolean;
  message: string | null;
  data: string | null;
}

interface DmnOperationResult {
  $id?: string;
  success: boolean;
  message: string | null;
  data: any;
}

interface UpdateDmnRuleRequest {
  ruleId: string;
  description?: string | null;
  inputValues: Array<string>;
  outputValues: Array<string>;
}

interface CreateDmnRuleRequest {
  description?: string | null;
  inputValues: Array<string>;
  outputValues: Array<string>;
}

/* ----- API Result Type ------ */

type ApiResult<T> =
  | { success: true; data: T }
  | { success: false; error: string };

/* ----- API Functions ------ */

//  ----- GET: Api/Dmn/table -----
export async function getAccessRules(): Promise<ApiResult<AccessRulesData>> {
  try {
    const data = await request<DmnDecisionTable>("Dmn/table");
    const rules = transformRulesToRuleColumns(data.rules ?? []);
    const info: DecisionInfo = {
      decisionId: data.decisionId ?? "",
      decisionName: data.decisionName ?? "",
      hitPolicy: data.hitPolicy ?? "",
    };

    return { success: true, data: { rules, info } };
  } catch (error) {
    return {
      success: false,
      error:
        error instanceof Error
          ? error.message
          : "Failed to fetch table for access rules",
    };
  }
}

//  ----- GET: Api/Dmn/validate -----
export async function validateAccessRules(): Promise<
  ApiResult<DmnValidateResponse>
> {
  try {
    const data = await request<{
      $id?: string;
      success: boolean;
      message: string | null;
      data: string | null;
    }>("Dmn/validate");

    return {
      success: true,
      data: {
        success: data.success,
        message: data.message,
        data: data.data,
      },
    };
  } catch (error) {
    return {
      success: false,
      error:
        error instanceof Error
          ? error.message
          : "Failed to validate access rules",
    };
  }
}

// ----- PUT: Api/Dmn/rules -----
export async function updateAccessRule(
  ruleId: string,
  data: {
    inputUser?: string;
    inputProject?: string;
    inputSubmissionId?: string;
    outputTag: string;
    outputValue: string;
    outputEnv: string;
    description?: string;
  }
): Promise<ApiResult<DmnOperationResult>> {
  try {
    const { inputValues, outputValues } = transformFormDataForApi(data);

    const requestBody: UpdateDmnRuleRequest = {
      ruleId,
      description: data.description ?? null,
      inputValues,
      outputValues,
    };

    const response = await request<DmnOperationResult>("Dmn/rules", {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(requestBody),
    });

    return {
      success: true,
      data: response,
    };
  } catch (error) {
    if (isNextRedirectError(error)) {
      throw error;
    }

    return {
      success: false,
      error:
        error instanceof Error
          ? error.message
          : "Failed to update access rule",
    };
  }
}

// ----- POST: Api/Dmn/rules -----
export async function createAccessRule(data: {
  inputUser?: string;
  inputProject?: string;
  outputTag: string;
  outputValue: string;
  outputEnv: string;
  description?: string;
}): Promise<ApiResult<DmnOperationResult>> {
  try {
    const { inputValues, outputValues } = transformFormDataForApi({
      ...data,
      inputSubmissionId: "", // New rules don't have submissionId
    });

    const requestBody: CreateDmnRuleRequest = {
      description: data.description ?? null,
      inputValues,
      outputValues,
    };

    const response = await request<DmnOperationResult>("Dmn/rules", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(requestBody),
    });

    return {
      success: true,
      data: response,
    };
  } catch (error) {
    if (isNextRedirectError(error)) {
      throw error;
    }

    return {
      success: false,
      error:
        error instanceof Error ? error.message : "Failed to create access rule",
    };
  }
}

// ----- DELETE: Api/Dmn/rules/{ruleId} -----
export async function deleteAccessRule(
  ruleId: string
): Promise<ApiResult<DmnOperationResult>> {
  try {
    const response = await request<DmnOperationResult>(`Dmn/rules/${ruleId}`, {
      method: "DELETE",
    });

    return {
      success: true,
      data: response,
    };
  } catch (error) {
    if (isNextRedirectError(error)) {
      throw error;
    }

    return {
      success: false,
      error:
        error instanceof Error ? error.message : "Failed to delete access rule",
    };
  }
}

// ----- POST: Api/Dmn/deploy -----
export async function deployAccessRules(): Promise<ApiResult<DmnOperationResult>> {
  try {
    const response = await request<DmnOperationResult>("Dmn/deploy", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: "",
    });

    return {
      success: true,
      data: response,
    };
  } catch (error) {
    if (isNextRedirectError(error)) {
      throw error;
    }

    return {
      success: false,
      error:
        error instanceof Error ? error.message : "Failed to deploy access rules",
    };
  }
}