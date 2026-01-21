"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { credentialsSchema, CredentialsFormData } from "@/lib/schema/update-credentials";
import { updateCredentialsAction } from "@/lib/actions/update-credentials-actions";

import SaveCredentialsButton from "./SaveCredentialsButton";
import CredentialsVisibilityToggle from "./CredentialsVisibilityToggle";
import { Input } from "@/components/ui/input";
import { Field, FieldGroup, FieldLabel, FieldSet, FieldError } from "@/components/ui/field";
import { CredentialType } from "@/types/update-credentials";
import CredentialsHelpTooltip from "./CredentialsHelpTooltip";

{/* Const and Types for the following:
  - Props for CredentialsForm component
  - Types for CredentialsFormProps
  - Constants for the Form Fields
  - Types for updating credentials based on Submission or Egress */}

type CredentialsFormProps = {
  type: CredentialType;
  onSuccess?: () => void;
};
const TITLE: Record<CredentialType, string> = {
  submission: "Update Credentials for Submission",
  egress: "Update Credentials for Egress",
};
const FIELDS = [
  {
    name: "username" as const,
    label: "Username",
    inputType: "text",
    placeholder: "Username",
    autoComplete: "username",
    isPassword: false,
  },
  {
    name: "password" as const,
    label: "Password",
    inputType: "password",
    placeholder: "Password",
    autoComplete: "new-password",
    isPassword: true,
  },
  {
    name: "confirmPassword" as const,
    label: "Confirm Password",
    inputType: "password",
    placeholder: "Confirm password",
    autoComplete: "new-password",
    isPassword: true,
  },
] as const;

export default function CredentialsForm({ type, onSuccess }: CredentialsFormProps) {
  {/* Constants for the following:
    - State to manage password visibility for each password field
    - State for loading
    - Initialize react-hook-form with zod resolver
    - Toggle visibility */}

  const [showPassword, setShowPassword] = useState<Record<string, boolean>>({});
  const [isLoading, setIsLoading] = useState(false);
  const {
    register,
    handleSubmit,
    formState: { errors },
    reset,
  } = useForm<CredentialsFormData>({
    resolver: zodResolver(credentialsSchema),
    defaultValues: {
      username: "",
      password: "",
      confirmPassword: "",
    },
  });
  const togglePasswordVisibility = (fieldName: string) => {
    setShowPassword((prev) => ({
      ...prev,
      [fieldName]: !prev[fieldName],
    }));
  };

  const onSubmit = async (data: CredentialsFormData) => {
    setIsLoading(true);

    try {
      const result = await updateCredentialsAction(type, data);

      if (!result.success) {
        toast.error(result.error);
        return;
      }

      reset();
      toast.success("Credentials updated successfully!");

      onSuccess?.();

    } catch (error) {
      toast.error("An unexpected error occurred");

    } finally {
      setIsLoading(false);
    }
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="mt-2 max-w-md">
      <FieldSet>
        {/* Form Title */}
        <div className="flex items-center">
          <h2 className="text-xl font-bold">{TITLE[type]}</h2>
          <CredentialsHelpTooltip type={type} />
        </div>

        {/* Form Fields */}
        <FieldGroup>
          {FIELDS.map((f) => {
            const id = `${type}-${f.name}`;
            const isVisible = showPassword[f.name];
            const error = errors[f.name];

            return (
              <Field key={id} data-invalid={error ? true : undefined}>
                <FieldLabel htmlFor={id}>{f.label}</FieldLabel>
                <div className="relative">
                  <Input
                    id={id}
                    type={f.isPassword && isVisible ? "text" : f.inputType}
                    placeholder={f.placeholder}
                    autoComplete={f.autoComplete}
                    aria-invalid={error ? true : undefined}
                    {...register(f.name)}
                  />

                  {/* Password Visibility Toggle for Password Fields */}
                  {f.isPassword && (
                    <CredentialsVisibilityToggle
                      isVisible={isVisible}
                      onToggle={() => togglePasswordVisibility(f.name)}
                    />
                  )}
                </div>
                {error && <FieldError>{error.message}</FieldError>}
              </Field>
            );
          })}

          {/* Save Button */}
          <div className="flex justify-end pb-4">
            <SaveCredentialsButton isLoading={isLoading} />
          </div>
        </FieldGroup>
      </FieldSet>
    </form>
  );
}
