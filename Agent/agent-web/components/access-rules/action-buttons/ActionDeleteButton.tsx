"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Trash2 } from "lucide-react";
import { RuleColumns } from "@/types/access-rules";


/* ----- Types ------ */

type ActionDeleteButtonProps = {
  rule: RuleColumns;
  onDelete: (rule: RuleColumns) => void;
};


/* ----- Delete Button Component for Access Rules ------ */

export default function ActionDeleteButton({
  rule,
  onDelete,
}: ActionDeleteButtonProps) {
  const [isOpen, setIsOpen] = useState(false);

  const handleConfirmDelete = () => {
    onDelete(rule);
    setIsOpen(false);
  };

  return (
    <>
    {/* Delete Button - Opens confirmation dialog when clicked */}
      <Button
        variant="ghost"
        size="icon"
        className="h-9 w-9 text-red-600 hover:text-red-700 hover:bg-red-50 hover:border-red-300 border border-transparent"
        onClick={() => setIsOpen(true)}
      >
        <Trash2 className="h-5 w-5" />
      </Button>

      {/* Confirmation Dialog - Prevents accidental deletions */}
      <AlertDialog open={isOpen} onOpenChange={setIsOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete Rule</AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want to delete rule{" "}
              <span className="font-semibold">{rule.outputTag}</span>?
            </AlertDialogDescription>
          </AlertDialogHeader>

          {/* Dialog Footer with action buttons */}
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleConfirmDelete}
              className="
                border-2 border-red-600
                bg-red-500
                text-white
                hover:bg-red-600
                hover:border-red-700
                transition-colors
              "
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
