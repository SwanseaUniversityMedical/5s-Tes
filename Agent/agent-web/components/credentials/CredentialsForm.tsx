"use client";

import { useState } from "react";
import SaveCredentialsButton from "./SaveCredentialsButton";
import CredentialsVisibilityToggle from "./CredentialsVisibilityToggle";
import { Input } from "@/components/ui/input";
import { Field, FieldGroup, FieldLabel, FieldSet } from "@/components/ui/field";
import { CredentialType } from "@/types/update-credentials";
import { Eye, EyeOff } from "lucide-react";

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
    label: "Username",
    inputType: "text",
    placeholder: "Username",
    autoComplete: "Username",
    isPassword: false,
  },
  {
    name: "password",
    label: "Password",
    inputType: "password",
    placeholder: "Password",
    autoComplete: "current-password",
    isPassword: true,
  },
  {
    name: "confirmPassword",
    label: "Confirm Password",
    inputType: "password",
    placeholder: "Confirm password",
    autoComplete: "new-password",
    isPassword: true,
  },
] as const;

// Creates Forms for Updating the Credentials based on Submission or Egress (Types)

export default function CredentialsForm({ type }: CredentialsFormProps) {


  {/* State to manage password visibility for each password field */}
  const [showPassword, setShowPassword] = useState<Record<string, boolean>>({});

  const togglePasswordVisibility = (fieldName: string) => {
    setShowPassword((prev) => ({
      ...prev,
      [fieldName]: !prev[fieldName],
    }));
  };

  return (
    <form className="mt-2 max-w-md">
      <FieldSet>
        {/* Form Title */}
        <h1 className="text-xl font-bold">{TITLE[type]}</h1>

        {/* Form Fields */}
        <FieldGroup>
          {FIELDS.map((f) => {
            const id = `${type}-${f.name}`;
            const isVisible = showPassword[f.name];

            return (
              <Field key={id}>
                <FieldLabel htmlFor={id}>{f.label}</FieldLabel>
                <div className="relative">
                  <Input
                    id={id}
                    name={f.name}
                    type={f.isPassword && isVisible ? "text" : f.inputType}
                    placeholder={f.placeholder}
                    autoComplete={f.autoComplete}
                    required
                  />

                  {/* Password Visibility Toggle for Password Fields */}
                  {f.isPassword && (
                    <CredentialsVisibilityToggle
                      isVisible={isVisible}
                      onToggle={() => togglePasswordVisibility(f.name)}
                    />
                  )}
                </div>
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
