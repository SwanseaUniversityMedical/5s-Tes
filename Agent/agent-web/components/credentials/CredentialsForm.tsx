"use client";

import SaveCredentialsButton from "./SaveCredentialsButton";
import { Input } from "@/components/ui/input";
import { Field, FieldGroup, FieldLabel, FieldSet } from "@/components/ui/field";
import { CredentialType } from "@/types/update-credentials";

// Props for CredentialsForm component

type CredentialsFormProps = {
  type: CredentialType;
};

// Titles for each Credential Type

const TITLE: Record<CredentialType, string> = {
  submission: "Update Credentials for Submission",
  egress: "Update Credentials for Egress",
};

// Constants for Form Fields

const FIELDS = [
  {
    name: "username",
    label: "Enter Keycloak username",
    inputType: "text",
    placeholder: "Username",
    autoComplete: "Username",
  },
  {
    name: "password",
    label: "Enter Keycloak password",
    inputType: "password",
    placeholder: "Password",
    autoComplete: "current-password",
  },
  {
    name: "confirmPassword",
    label: "Confirm Keycloak password",
    inputType: "password",
    placeholder: "Confirm password",
    autoComplete: "new-password",
  },
] as const;

// Creates Forms for Updating the Credentials based on Submission or Egress (Types)

export default function CredentialsForm({ type }: CredentialsFormProps) {
  return (
    <form className="mt-2 max-w-md">
      <FieldSet>
        {/* Form Title */}
        <h1 className="text-xl font-bold">{TITLE[type]}</h1>

        {/* Form Fields */}
        <FieldGroup>
          {FIELDS.map((f) => {
            const id = `${type}-${f.name}`;

            return (
              <Field key={id}>
                <FieldLabel htmlFor={id}>{f.label}</FieldLabel>
                <Input
                  id={id}
                  name={f.name}
                  type={f.inputType}
                  placeholder={f.placeholder}
                  autoComplete={f.autoComplete}
                />
              </Field>
            );
          })}

          {/* Save Button */}
          <div className="flex justify-end">
            <SaveCredentialsButton />
          </div>
        </FieldGroup>
      </FieldSet>
    </form>
  );
}
