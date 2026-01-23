import { z } from "zod";

// Define Credential Types

export type CredentialType = "submission" | "egress";

// Zod Schema for Validating Credentials Form Data

export const credentialsSchema = z
  .object({
    username: z
      .string()
      .min(1, "Username is required")
      .min(3, "Username must be at least 3 characters"),

    password: z
      .string()
      .min(1, "Password is required")
      .min(8, "Password must be at least 8 characters"),

    confirmPassword: z
      .string()
      .min(1, "Please confirm your password"),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "Passwords do not match",
    path: ["confirmPassword"],
  });

// Define Form Data Structure for Credentials

  export type CredentialsFormData = z.infer<typeof credentialsSchema>;

// Define Response Type from Update Credentials API

export type UpdateCredentialsResponse = {
  error?: boolean;
  errorMessage?: string | null;
  id?: number;
  userName: string;
  passwordEnc: string;
  confirmPassword?: string | null;
  credentialType?: number;
  valid?: boolean;
};