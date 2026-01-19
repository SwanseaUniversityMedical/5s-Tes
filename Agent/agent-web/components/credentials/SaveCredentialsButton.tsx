import { Button } from "@/components/ui/button";
import { Loader2 } from "lucide-react";

// Props for SaveCredentialsButton component

type SaveCredentialsButtonProps = {
  label?: string;
  isLoading?: boolean;
  disabled?: boolean;
};

// Creates a Save Button for Credentials Form

export default function SaveCredentialsButton({
  label = "Save",
  isLoading = false,
  disabled = false,
}: SaveCredentialsButtonProps) {
  return (
    <div className="flex justify-end">
      <Button
        type="submit"
        disabled={isLoading || disabled}
        className="
        transition-all
        duration-200
        hover:bg-gray-800
        hover:shadow-md
        hover:scale-105"
      >
        {isLoading ? (
        <>
          <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          Saving...
        </>
      ) : (
        label
      )}
      </Button>
    </div>
  );
}
