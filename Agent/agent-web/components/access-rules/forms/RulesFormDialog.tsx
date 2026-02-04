"use client";

import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ruleFormSchema, RuleFormData } from "@/types/access-rules";

/* ----- Types ------ */

type DialogMode = "add" | "edit";

type RuleFormDialogProps = {
  isOpen: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (data: RuleFormData) => Promise<boolean>; // Returns true if successful
  mode: DialogMode;
  initialData?: RuleFormData;
};

type FormFieldConfig = {
  key: keyof RuleFormData;
  label: string;
  placeholder: string;
};

type FieldCategory = {
  title: string;
  fields: FormFieldConfig[];
};

/* ----- Constants ------ */

// Categorized fields with original column names
const FIELD_CATEGORIES: FieldCategory[] = [
  {
    title: "Input Values",
    fields: [
      {
        key: "inputUser",
        label: "Input: user",
        placeholder: "Enter user input",
      },
      {
        key: "inputProject",
        label: "Input: project",
        placeholder: "Enter project input",
      },
    ],
  },
  {
    title: "Output Values",
    fields: [
      {
        key: "outputTag",
        label: "Output: tag",
        placeholder: "Enter output tag",
      },
      {
        key: "outputValue",
        label: "Output: value",
        placeholder: "Enter output value",
      },
      {
        key: "outputEnv",
        label: "Output: env",
        placeholder: "Enter output environment",
      },
    ],
  },
  {
    title: "Optional",
    fields: [
      {
        key: "description",
        label: "Description",
        placeholder: "Enter rule description",
      },
    ],
  },
];

const DEFAULT_VALUES: RuleFormData = {
  inputUser: "",
  inputProject: "",
  outputTag: "",
  outputValue: "",
  outputEnv: "",
  description: "-",
};

const DIALOG_CONFIG = {
  add: {
    title: "Add New Rule",
    description: "Fill in the details below to create a new access rule.",
    submitLabel: "Add Rule",
  },
  edit: {
    title: "Edit Rule",
    description: "Make changes to the rule below. Click save when you're done.",
    submitLabel: "Save Changes",
  },
};

/* ----- Access Rules Form Components ----- */

export default function RuleFormDialog({
  isOpen,
  onOpenChange,
  onSubmit,
  mode,
  initialData,
}: RuleFormDialogProps) {
  const {
    register,
    reset,
    handleSubmit,
    formState: { errors, isDirty, isSubmitting, isSubmitted },
  } = useForm<RuleFormData>({
    resolver: zodResolver(ruleFormSchema),
    defaultValues: DEFAULT_VALUES,
    mode: "onSubmit",
    reValidateMode: "onChange",
  });

  // Reset form when dialog opens
  useEffect(() => {
    if (!isOpen) return;

    const nextValues =
      mode === "edit" ? (initialData ?? DEFAULT_VALUES) : DEFAULT_VALUES;
    reset(nextValues, { keepDirty: false, keepTouched: false });
  }, [isOpen, mode, initialData, reset]);

  const submit = handleSubmit(async (data) => {
    const success = await onSubmit(data);
    
    // Only close dialog and reset form if the operation was successful
    if (success) {
      reset(DEFAULT_VALUES);
      onOpenChange(false);
    }
  });

  const handleCancel = () => {
    reset(DEFAULT_VALUES);
    onOpenChange(false);
  };

  // Get dialog config based on mode
  const config = DIALOG_CONFIG[mode];

  // Render a single form field
  const renderField = ({ key, label, placeholder }: FormFieldConfig) => {
    const message = errors[key]?.message;

    return (
      <div key={key} className="grid grid-cols-4 items-start gap-4">
        <Label htmlFor={key} className="text-right text-sm pt-2">
          {label}
        </Label>

        <div className="col-span-3 space-y-1">
          <Input
            id={key}
            placeholder={placeholder}
            aria-invalid={!!message}
            className={
              message ? "border-destructive focus-visible:ring-destructive" : ""
            }
            {...register(key)}
          />
          {message ? (
            <p className="text-xs text-destructive">{String(message)}</p>
          ) : null}
        </div>
      </div>
    );
  };

  // Check if there are any validation errors
  const hasErrors = Object.keys(errors).length > 0;

  // Disable submit if form is submitting or in edit mode without changes
  const disableSubmit = isSubmitting || (mode === "edit" && !isDirty);

  // Render category header with lines
  const renderCategoryHeader = (title: string) => (
    <div className="flex items-center gap-3 ">
      <div className="flex-1 h-px bg-border" />
      <span className="text-sm font-semibold text-muted-foreground">
        ({title})
      </span>
      <div className="flex-1 h-px bg-border" />
    </div>
  );

  return (
    <Dialog open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-125">
        {/* Dialog Header */}
        <DialogHeader>
          <DialogTitle>{config.title}</DialogTitle>
          <DialogDescription>{config.description}</DialogDescription>
        </DialogHeader>
        <form onSubmit={submit}>
          {/* Form Content */}
          <div className="space-y-4 py-4">
            {isSubmitted && hasErrors ? (
              <div className="rounded-md border border-destructive/40 bg-destructive/10 p-3">
                <p className="text-sm text-destructive">
                  Please fix the highlighted fields.
                </p>
              </div>
            ) : null}
            {FIELD_CATEGORIES.map((category) => (
              <div key={category.title} className="space-y-3">
                {renderCategoryHeader(category.title)}
                <div className="space-y-4 p-4 bg-muted/50 rounded-lg">
                  {category.fields.map(renderField)}
                </div>
              </div>
            ))}
          </div>

          {/* Dialog Footer */}
          <DialogFooter>
            <Button type="button" variant="outline" onClick={handleCancel}>
              Cancel
            </Button>
            <Button
              type="submit"
              disabled={disableSubmit}
              className="
                border-2 border-blue-600
                bg-blue-500
                text-white
                hover:bg-blue-600
                hover:border-blue-700
                transition-colors
                disabled:opacity-50
                disabled:cursor-not-allowed
              "
            >
              {config.submitLabel}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
