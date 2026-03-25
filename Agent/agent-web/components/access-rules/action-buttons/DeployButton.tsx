"use client";

import { useState } from "react";
import { Rocket } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { deployAccessRules } from "@/api/access-rules";

/* ----- Deploy Button Component for Access Rules ------ */
export default function DeployButton() {
  const [isDeploying, setIsDeploying] = useState(false);
  const [isDialogOpen, setIsDialogOpen] = useState(false);

  const handleDeploy = async () => {
    setIsDeploying(true);
    setIsDialogOpen(false);

    // Add delay for visual feedback
    await new Promise((resolve) => setTimeout(resolve, 1000));

    const result = await deployAccessRules();
    if (result.success) {
      toast.success(result.data.message || "Rules deployed successfully");
    } else {
      toast.error(result.error || "Failed to deploy rules");
    }

    setIsDeploying(false);
  };

  // Render Deploy Button with Confirmation Dialog

  return (
    <AlertDialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
      <Tooltip>
        <TooltipTrigger asChild>
          <AlertDialogTrigger asChild>
            <Button
              size="sm"
              variant="outline"
              className="border-green-600 bg-green-50 text-green-600 hover:bg-green-100 hover:border-green-600 hover:text-green-700"
              disabled={isDeploying}
            >
              <Rocket
                className={`h-4 w-4 mr-1.5 ${isDeploying ? "animate-pulse" : ""}`}
              />
              {isDeploying ? "Deploying..." : "Deploy"}
            </Button>
          </AlertDialogTrigger>
        </TooltipTrigger>
        <TooltipContent>Deploy rules to production</TooltipContent>
      </Tooltip>

      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Deploy Access Rules</AlertDialogTitle>
          <AlertDialogDescription>
            <span className="font-semibold">
              Are you sure you want to deploy these rules to production?
            </span>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction
            onClick={handleDeploy}
            className="bg-green-600 hover:bg-green-700"
          >
            Yes, Deploy
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
