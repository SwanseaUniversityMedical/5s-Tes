"use server";

import { isNextRedirectError } from "@/lib/api/helpers";
import request from "@/lib/api/request";
import {
  DecisionInfo,
  AccessRulesData,
  DmnDecisionTable,
  DmnValidateResponse,
  DmnOperationResult,
  CreateDmnRuleRequest,
  UpdateDmnRuleRequest,
  UpdateAccessRuleDto,
  CreateAccessRuleDto,
} from "@/types/access-rules";
import { ActionResult } from "@/types/ActionResult";
import {
  transformRulesToRuleColumns,
  transformFormDataForApi,
} from "@/lib/helpers/access-rules-api";


/* ----- Helper Functions ----- */

async function handleRequest<T>(
  requestPromise: Promise<T>,
): Promise<ActionResult<T>> {
  try {
    const data = await requestPromise;
    return { success: true, data };
  } catch (error) {
    if (isNextRedirectError(error)) {
      throw error;
    }
    return {
      success: false,
      error:
        error instanceof Error ? error.message : "An unexpected error occurred",
    };
  }
}

/* ----- Fetch Keys for target API endpoints ----- */

const fetchKeys = {
  getTable: () => `Dmn/table`,
  validate: () => `Dmn/validate`,
  rules: () => `Dmn/rules`,
  ruleById: (ruleId: string) => `Dmn/rules/${ruleId}`,
  deploy: () => `Dmn/deploy`,
};

/* ----- API Functions ------ */

//  ----- GET: Api/Dmn/table -----
export async function getAccessRules(): Promise<ActionResult<AccessRulesData>> {
  return handleRequest(
    request<DmnDecisionTable>(fetchKeys.getTable()).then((data) => {
      const rules = transformRulesToRuleColumns(data.rules ?? []);
      const info: DecisionInfo = {
        decisionId: data.decisionId ?? "",
        decisionName: data.decisionName ?? "",
        hitPolicy: data.hitPolicy ?? "",
      };

      return { rules, info };
    }),
  );
}

//  ----- GET: Api/Dmn/validate -----
export async function validateAccessRules(): Promise<
  ActionResult<DmnValidateResponse>
> {
  return handleRequest(request<DmnValidateResponse>(fetchKeys.validate()));
}

// ----- PUT: Api/Dmn/rules -----
export async function updateAccessRule(
  ruleId: string,
  data: UpdateAccessRuleDto,
): Promise<ActionResult<DmnOperationResult>> {
  const { inputValues, outputValues } = transformFormDataForApi(data);

  const requestBody: UpdateDmnRuleRequest = {
    ruleId,
    description: data.description ?? null,
    inputValues,
    outputValues,
  };

  return handleRequest(
    request<DmnOperationResult>(fetchKeys.rules(), {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(requestBody),
    }),
  );
}

// ----- POST: Api/Dmn/rules -----
export async function createAccessRule(
  data: CreateAccessRuleDto,
): Promise<ActionResult<DmnOperationResult>> {
  const { inputValues, outputValues } = transformFormDataForApi({
    ...data,
    inputSubmissionId: "", // New rules don't have submissionId
  });

  const requestBody: CreateDmnRuleRequest = {
    description: data.description ?? null,
    inputValues,
    outputValues,
  };

  return handleRequest(
    request<DmnOperationResult>(fetchKeys.rules(), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(requestBody),
    }),
  );
}

// ----- DELETE: Api/Dmn/rules/{ruleId} -----
export async function deleteAccessRule(
  ruleId: string,
): Promise<ActionResult<DmnOperationResult>> {
  return handleRequest(
    request<DmnOperationResult>(fetchKeys.ruleById(ruleId), {
      method: "DELETE",
    }),
  );
}

// ----- POST: Api/Dmn/deploy -----
export async function deployAccessRules(): Promise<
  ActionResult<DmnOperationResult>
> {
  return handleRequest(
    request<DmnOperationResult>(fetchKeys.deploy(), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: "",
    }),
  );
}
