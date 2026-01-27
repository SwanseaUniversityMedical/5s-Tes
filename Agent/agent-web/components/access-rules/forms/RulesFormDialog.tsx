"use client";

import { useState, useEffect } from "react";
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
import { RuleColumns } from "@/types/access-rules";

/* ----- Types ------ */

type RuleFormData = RuleColumns;

type DialogMode = "add" | "edit";

type RuleFormDialogProps = {
  isOpen: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (data: RuleFormData) => void;
  mode: DialogMode;
  initialData?: RuleFormData;
};

type FormField = {
  key: keyof RuleFormData;
  label: string;
  placeholder: string;
};

type FieldCategory = {
  title: string;
  fields: FormField[];
};

/* ----- Constants ------ */

// General fields (no category)
const GENERAL_FIELDS: FormField[] = [
  {
    key: "description",
    label: "Description",
    placeholder: "Enter rule description",
  },
];

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
];

const INITIAL_FORM_STATE: RuleFormData = {
  description: "",
  inputUser: "",
  inputProject: "",
  outputTag: "",
  outputValue: "",
  outputEnv: "",
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
  const [formData, setFormData] = useState<RuleFormData>(INITIAL_FORM_STATE);

  // Reset form when dialog opens
  useEffect(() => {
    if (isOpen) {
      setFormData(initialData ?? INITIAL_FORM_STATE);
    }
  }, [isOpen, initialData]);

  const handleFieldChange = (key: keyof RuleFormData, value: string) => {
    setFormData((prev) => ({
      ...prev,
      [key]: value,
    }));
  };

  const handleSubmit = () => {
    onSubmit(formData);
    setFormData(INITIAL_FORM_STATE);
    onOpenChange(false);
  };

  const handleCancel = () => {
    setFormData(INITIAL_FORM_STATE);
    onOpenChange(false);
  };

  // Check if form has any data (for add mode validation)
  const isFormEmpty = Object.values(formData).every((value) => value === "");

  // Get dialog config based on mode
  const config = DIALOG_CONFIG[mode];

  // Render a single form field
  const renderField = ({ key, label, placeholder }: FormField) => (
    <div key={key} className="grid grid-cols-4 items-center gap-4">
      <Label htmlFor={key} className="text-right text-sm">
        {label}
      </Label>
      <Input
        id={key}
        value={formData[key]}
        onChange={(e) => handleFieldChange(key, e.target.value)}
        placeholder={placeholder}
        className="col-span-3"
      />
    </div>
  );

  // Render category header with lines
  const renderCategoryHeader = (title: string) => (
    <div className="flex items-center gap-3 ">
      <div className="flex-1 h-px bg-border" />
      <span className="text-sm font-medium text-muted-foreground">({title})</span>
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

        {/* Form Content */}
        <div className="space-y-4 py-4">
          {GENERAL_FIELDS.map(renderField)}
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
          <Button variant="outline" onClick={handleCancel}>
            Cancel
          </Button>
          <Button
            onClick={handleSubmit}
            disabled={mode === "add" && isFormEmpty}
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
      </DialogContent>
    </Dialog>
  );
}
