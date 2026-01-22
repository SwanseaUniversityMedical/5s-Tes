"use server";

import { isNextRedirectError } from "@/lib/api/helpers";
import request  from "@/lib/api/request";
import type { CredentialType, CredentialsFormData, UpdateCredentialsResponse } from "@/types/update-credentials";

// Action result type
export type ActionResult<T> =
  | { success: true; data: T }
  | { success: false; error: string };

// API endpoints
const CHECK_ENDPOINTS: Record<CredentialType, string> = {
  submission: "SubmissionCredentials/CheckCredentialsAreValid",
  egress: "DataEgressCredentials/CheckCredentialsAreValid",
};

const UPDATE_ENDPOINTS: Record<CredentialType, string> = {
  submission: "SubmissionCredentials/UpdateCredentials",
  egress: "DataEgressCredentials/UpdateCredentials",
};



/* Server action to check if credentials are valid */
export async function checkCredentialsValid(
  type: CredentialType
): Promise<ActionResult<boolean>> {

  try {
    const response = await request(CHECK_ENDPOINTS[type], {
      method: "GET",
    });

    return { success: true, data: response.result };

  } catch (error) {
    if (isNextRedirectError(error)) {
      throw error;
    }

    return {
      success: false,
      error: error instanceof Error ? error.message : "An unexpected error occurred",
    };
  }
}




/* Server action for updating credentials */
export async function updateCredentials(
  type: CredentialType,
  formData: CredentialsFormData
): Promise<ActionResult<UpdateCredentialsResponse>> {


  try {
    const response = await request<UpdateCredentialsResponse>(UPDATE_ENDPOINTS[type], {
      method: "POST",
      headers: {
        "Content-Type": "application/json-patch+json",
      },
      body: JSON.stringify({
        userName: formData.username,
        passwordEnc: formData.password,
        confirmPassword: formData.confirmPassword,
        credentialType: 0,
      }),
    });

    // Backend returns valid: false if credentials are invalid
    if (response.error || response.valid === false) {
      const errorMsg = response.errorMessage || "Invalid credentials";
      return {
        success: false,
        error: errorMsg,
      };
    }
    return { success: true, data: response };

  } catch (error) {
    if (isNextRedirectError(error)) {
      throw error;
    }

    return {
      success: false,
      error: error instanceof Error ? error.message : "An unexpected error occurred",
    };
  }
}
