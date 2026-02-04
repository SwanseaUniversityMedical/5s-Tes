"use client";

import { useCallback, useRef } from "react";
import { toast } from "sonner";

/**
 Custom hook to manage persistent error toasts that
 dismiss when dialog closes.

 Usage:
 -------
 - const { showError, dismissError, handleOpenChange } = useErrorToast();

    - showError("Something went wrong");
    - dismissError();

    - // In dialog onOpenChange:
    - <Dialog open={isOpen} onOpenChange={handleOpenChange(setIsOpen)} />
 -------

 Returns:
 ---------
 - showError: Function to show a persistent error toast.
 - dismissError: Function to dismiss the current error toast.
 - handleOpenChange: Wrapper for dialog onOpenChange to dismiss toast on close.

 Example:
 ---------
 <Dialog open={isOpen} onOpenChange={handleOpenChange(setIsOpen)} />
 */
export function useErrorToast() {
	const errorToastId = useRef<string | number | null>(null);

	// Dismiss the current error toast if any
	const dismissError = useCallback(() => {
		if (errorToastId.current) {
			toast.dismiss(errorToastId.current);
			errorToastId.current = null;
		}
	}, []);

	// Show a persistent error toast (dismisses any previous one first)
	const showError = useCallback(
		(message: string) => {
			dismissError();
			errorToastId.current = toast.error(message, { duration: Infinity });
		},
		[dismissError],
	);

	// Wrapper for onOpenChange that dismisses toast when dialog closes
	const handleOpenChange = useCallback(
		(setOpen: (open: boolean) => void) => (open: boolean) => {
			if (!open) {
				dismissError();
			}
			setOpen(open);
		},
		[dismissError],
	);

	return { showError, dismissError, handleOpenChange };
}
