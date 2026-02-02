import {
  CheckCircle2,
  XCircle,
  AlertTriangle,
  Sparkles,
  Rocket,
  Save,
  Plus,
  LucideIcon,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

/* ----- Types ------ */

export type OperationStatus = "processing" | "success" | "error";

export type OperationError = {
  field?: string;
  message: string;
  code?: string;
};

export type OperationType = "add" | "edit" | "deploy" | "save";

type ColorScheme = {
  processing: {
    container: string;
    icon: string;
    iconBg: string;
    orbit: string[];
    ring: string;
  };
  success: {
    container: string;
    icon: string;
    iconBg: string;
    badge: string;
    badgeDot: string;
  };
  error: {
    container: string;
    icon: string;
    iconBg: string;
    list: string;
    listItem: string;
    listBorder: string;
    listDot: string;
    listField: string;
  };
};

type OperationConfig = {
  processingTitle: string;
  processingDescription: string;
  successTitle: string;
  successDescription: string;
  successBadge: string;
  errorTitle: string;
  errorDescription: string;
  cancelLabel: string;
  retryLabel: string;
  Icon: LucideIcon;
  colors: ColorScheme;
};

type OperationStatusProps = {
  status: OperationStatus;
  type: OperationType;
  errors?: OperationError[];
  message?: string;
  onCancel: () => void;
  onRetry: () => void;
  customConfig?: Partial<OperationConfig>;
};

/* ----- Color Schemes ------ */

const COLOR_SCHEMES: Record<string, ColorScheme> = {
  blue: {
    processing: {
      container: "border-blue-200 bg-gradient-to-b from-blue-50/80 to-white",
      icon: "text-blue-500",
      iconBg: "bg-blue-100",
      orbit: ["bg-blue-400", "bg-blue-300", "bg-blue-500"],
      ring: "border-blue-200",
    },
    success: {
      container: "border-green-200 bg-gradient-to-b from-green-50/80 to-white",
      icon: "text-green-600",
      iconBg: "bg-green-100",
      badge: "text-green-700 bg-green-100/60",
      badgeDot: "bg-green-500",
    },
    error: {
      container: "border-red-200 bg-gradient-to-b from-red-50/80 to-white",
      icon: "text-red-600",
      iconBg: "bg-red-100",
      list: "text-red-700",
      listItem: "text-red-600 bg-red-50",
      listBorder: "border-red-100",
      listDot: "bg-red-400",
      listField: "text-red-700",
    },
  },
  purple: {
    processing: {
      container:
        "border-purple-200 bg-gradient-to-b from-purple-50/80 to-white",
      icon: "text-purple-500",
      iconBg: "bg-purple-100",
      orbit: ["bg-purple-400", "bg-purple-300", "bg-purple-500"],
      ring: "border-purple-200",
    },
    success: {
      container: "border-green-200 bg-gradient-to-b from-green-50/80 to-white",
      icon: "text-green-600",
      iconBg: "bg-green-100",
      badge: "text-green-700 bg-green-100/60",
      badgeDot: "bg-green-500",
    },
    error: {
      container: "border-red-200 bg-gradient-to-b from-red-50/80 to-white",
      icon: "text-red-600",
      iconBg: "bg-red-100",
      list: "text-red-700",
      listItem: "text-red-600 bg-red-50",
      listBorder: "border-red-100",
      listDot: "bg-red-400",
      listField: "text-red-700",
    },
  },
  orange: {
    processing: {
      container:
        "border-orange-200 bg-gradient-to-b from-orange-50/80 to-white",
      icon: "text-orange-500",
      iconBg: "bg-orange-100",
      orbit: ["bg-orange-400", "bg-orange-300", "bg-orange-500"],
      ring: "border-orange-200",
    },
    success: {
      container: "border-green-200 bg-gradient-to-b from-green-50/80 to-white",
      icon: "text-green-600",
      iconBg: "bg-green-100",
      badge: "text-green-700 bg-green-100/60",
      badgeDot: "bg-green-500",
    },
    error: {
      container: "border-red-200 bg-gradient-to-b from-red-50/80 to-white",
      icon: "text-red-600",
      iconBg: "bg-red-100",
      list: "text-red-700",
      listItem: "text-red-600 bg-red-50",
      listBorder: "border-red-100",
      listDot: "bg-red-400",
      listField: "text-red-700",
    },
  },
  green: {
    processing: {
      container:
        "border-emerald-200 bg-gradient-to-b from-emerald-50/80 to-white",
      icon: "text-emerald-500",
      iconBg: "bg-emerald-100",
      orbit: ["bg-emerald-400", "bg-emerald-300", "bg-emerald-500"],
      ring: "border-emerald-200",
    },
    success: {
      container: "border-green-200 bg-gradient-to-b from-green-50/80 to-white",
      icon: "text-green-600",
      iconBg: "bg-green-100",
      badge: "text-green-700 bg-green-100/60",
      badgeDot: "bg-green-500",
    },
    error: {
      container: "border-red-200 bg-gradient-to-b from-red-50/80 to-white",
      icon: "text-red-600",
      iconBg: "bg-red-100",
      list: "text-red-700",
      listItem: "text-red-600 bg-red-50",
      listBorder: "border-red-100",
      listDot: "bg-red-400",
      listField: "text-red-700",
    },
  },
};

/* ----- Operation Configurations ------ */

const OPERATION_CONFIGS: Record<OperationType, OperationConfig> = {
  add: {
    processingTitle: "Adding Rule",
    processingDescription: "Please wait while we add your new rule...",
    successTitle: "Rule Added",
    successDescription: "Your new rule has been added successfully.",
    successBadge: "Finalizing...",
    errorTitle: "Failed to Add Rule",
    errorDescription: "There were issues adding your rule.",
    cancelLabel: "Go Back & Edit",
    retryLabel: "Retry",
    Icon: Plus,
    colors: COLOR_SCHEMES.blue,
  },
  edit: {
    processingTitle: "Saving Changes",
    processingDescription: "Please wait while we save your changes...",
    successTitle: "Changes Saved",
    successDescription: "Your changes have been saved successfully.",
    successBadge: "Finalizing...",
    errorTitle: "Failed to Save Changes",
    errorDescription: "There were issues saving your changes.",
    cancelLabel: "Go Back & Edit",
    retryLabel: "Retry",
    Icon: Save,
    colors: COLOR_SCHEMES.purple,
  },
  deploy: {
    processingTitle: "Deploying Rules",
    processingDescription:
      "Please wait while we deploy your rules to production...",
    successTitle: "Deployment Successful",
    successDescription: "Your rules have been deployed successfully.",
    successBadge: "Live in production",
    errorTitle: "Deployment Failed",
    errorDescription: "There were issues deploying your rules.",
    cancelLabel: "Cancel",
    retryLabel: "Retry Deployment",
    Icon: Rocket,
    colors: COLOR_SCHEMES.orange,
  },
  save: {
    processingTitle: "Saving",
    processingDescription: "Please wait while we save your data...",
    successTitle: "Saved Successfully",
    successDescription: "Your data has been saved.",
    successBadge: "Complete",
    errorTitle: "Save Failed",
    errorDescription: "There were issues saving your data.",
    cancelLabel: "Cancel",
    retryLabel: "Retry",
    Icon: Save,
    colors: COLOR_SCHEMES.green,
  },
};

/* ----- Animated Loader Component ------ */

function AnimatedLoader({
  config,
  Icon,
}: {
  config: OperationConfig;
  Icon: LucideIcon;
}) {
  const colors = config.colors.processing;

  return (
    <div className="relative flex items-center justify-center h-16 w-16">
      {/* Center icon */}
      <Icon className={cn("h-8 w-8 animate-pulse", colors.icon)} />

      {/* Orbiting dots */}
      <div className="absolute inset-0 animate-spin [animation-duration:3s]">
        <div
          className={cn(
            "absolute top-0 left-1/2 -translate-x-1/2 h-2 w-2 rounded-full",
            colors.orbit[0],
          )}
        />
        <div
          className={cn(
            "absolute bottom-0 left-1/2 -translate-x-1/2 h-2 w-2 rounded-full",
            colors.orbit[1],
          )}
        />
      </div>

      <div className="absolute inset-0 animate-spin [animation-duration:2s] [animation-direction:reverse]">
        <div
          className={cn(
            "absolute top-1/2 left-0 -translate-y-1/2 h-1.5 w-1.5 rounded-full",
            colors.orbit[2],
          )}
        />
        <div
          className={cn(
            "absolute top-1/2 right-0 -translate-y-1/2 h-1.5 w-1.5 rounded-full",
            colors.orbit[0],
          )}
        />
      </div>

      {/* Glowing ring */}
      <div
        className={cn(
          "absolute inset-0 rounded-full border-2 animate-ping opacity-30",
          colors.ring,
        )}
      />
    </div>
  );
}

/* ----- Success Icon Component ------ */

function SuccessIcon({ config }: { config: OperationConfig }) {
  const colors = config.colors.success;

  return (
    <div className="relative flex items-center justify-center">
      <div
        className={cn(
          "absolute h-16 w-16 rounded-full animate-ping opacity-20",
          colors.iconBg,
        )}
      />
      <div
        className={cn(
          "relative flex h-14 w-14 items-center justify-center rounded-full",
          colors.iconBg,
        )}
      >
        <CheckCircle2 className={cn("h-8 w-8", colors.icon)} />
      </div>
    </div>
  );
}

/* ----- Error Icon Component ------ */

function ErrorIconDisplay({ config }: { config: OperationConfig }) {
  const colors = config.colors.error;

  return (
    <div className="relative flex items-center justify-center">
      <div
        className={cn(
          "relative flex h-14 w-14 items-center justify-center rounded-full",
          colors.iconBg,
        )}
      >
        <XCircle className={cn("h-8 w-8", colors.icon)} />
      </div>
    </div>
  );
}

/* ----- Processing State ------ */

function ProcessingState({ config }: { config: OperationConfig }) {
  return (
    <div className="flex flex-col items-center gap-5">
      <AnimatedLoader config={config} Icon={config.Icon} />
      <div className="text-center space-y-2">
        <h3 className="text-lg font-semibold text-gray-900">
          {config.processingTitle}
        </h3>
        <p className="text-sm text-muted-foreground">
          {config.processingDescription}
        </p>
      </div>
    </div>
  );
}

/* ----- Success State ------ */

function SuccessState({ config }: { config: OperationConfig }) {
  const colors = config.colors.success;

  return (
    <div className="flex flex-col items-center gap-4">
      <SuccessIcon config={config} />
      <div className="text-center space-y-2">
        <h3 className="text-lg font-semibold text-gray-900">
          {config.successTitle}
        </h3>
        <p className="text-sm text-muted-foreground">
          {config.successDescription}
        </p>
      </div>
      <div
        className={cn(
          "flex items-center gap-2 text-sm px-4 py-2 rounded-lg",
          colors.badge,
        )}
      >
        <div
          className={cn(
            "h-1.5 w-1.5 rounded-full animate-pulse",
            colors.badgeDot,
          )}
        />
        <span>{config.successBadge}</span>
      </div>
    </div>
  );
}

/* ----- Error State ------ */

function ErrorState({
  config,
  errors,
  message,
}: {
  config: OperationConfig;
  errors?: OperationError[];
  message?: string;
}) {
  const colors = config.colors.error;

  return (
    <div className="flex flex-col items-center gap-4">
      <ErrorIconDisplay config={config} />
      <div className="text-center space-y-2">
        <h3 className="text-lg font-semibold text-gray-900">
          {config.errorTitle}
        </h3>
        <p className="text-sm text-muted-foreground">
          {message || config.errorDescription}
        </p>
      </div>

      {/* Error List */}
      {errors && errors.length > 0 && (
        <div className="w-full mt-2 space-y-3">
          <div
            className={cn(
              "flex items-center gap-2 text-sm font-medium",
              colors.list,
            )}
          >
            <AlertTriangle className="h-4 w-4" />
            <span>Issues found ({errors.length}):</span>
          </div>
          <ul className="space-y-2 text-sm">
            {errors.map((error, index) => (
              <li
                key={index}
                className={cn(
                  "flex items-start gap-3 rounded-lg p-3 border",
                  colors.listItem,
                  colors.listBorder,
                )}
              >
                <span
                  className={cn(
                    "mt-1 h-2 w-2 rounded-full flex-shrink-0",
                    colors.listDot,
                  )}
                />
                <span>
                  {error.field && (
                    <span className={cn("font-semibold", colors.listField)}>
                      {error.field}:{" "}
                    </span>
                  )}
                  {error.message}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

/* ----- Action Buttons ------ */

function ActionButtons({
  config,
  onCancel,
  onRetry,
}: {
  config: OperationConfig;
  onCancel: () => void;
  onRetry: () => void;
}) {
  return (
    <div className="mt-6 flex justify-center gap-3">
      <Button
        variant="outline"
        onClick={onCancel}
        className="border-gray-300 hover:bg-gray-50"
      >
        {config.cancelLabel}
      </Button>
      <Button
        onClick={onRetry}
        className="bg-blue-500 hover:bg-blue-600 text-white border-0"
      >
        {config.retryLabel}
      </Button>
    </div>
  );
}

/* ----- Main Component ------ */

export default function OperationStatus({
  status,
  type,
  errors,
  message,
  onCancel,
  onRetry,
  customConfig,
}: OperationStatusProps) {
  // Merge default config with custom overrides
  const baseConfig = OPERATION_CONFIGS[type];
  const config: OperationConfig = {
    ...baseConfig,
    ...customConfig,
    colors: customConfig?.colors || baseConfig.colors,
  };

  // Get container class based on status
  const containerClass =
    status === "processing"
      ? config.colors.processing.container
      : status === "success"
        ? config.colors.success.container
        : config.colors.error.container;

  return (
    <div
      className={cn(
        "rounded-xl border-2 p-8 transition-all duration-300",
        containerClass,
      )}
    >
      {status === "processing" && <ProcessingState config={config} />}
      {status === "success" && <SuccessState config={config} />}
      {status === "error" && (
        <>
          <ErrorState config={config} errors={errors} message={message} />
          <ActionButtons
            config={config}
            onCancel={onCancel}
            onRetry={onRetry}
          />
        </>
      )}
    </div>
  );
}

/* ----- Export Types ------ */

export type { OperationConfig, ColorScheme };
