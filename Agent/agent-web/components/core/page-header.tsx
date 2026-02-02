import { cn } from "@/lib/utils";


/* ----- Types ------ */
type PageHeaderProps = {
  title: string;
  description?: React.ReactNode;
  action?: React.ReactNode;
  className?: string;
};

/* ----- Page Header Component ------ */
export function PageHeader({ title, description, action, className }: PageHeaderProps) {
  return (
    <div className={cn("mb-6", className)}>
      <div className="flex items-center gap-4">
        <h1 className="text-2xl font-bold">{title}</h1>
        {action}
      </div>
      {description && (
        <p className="mt-2 text-gray-600 dark:text-gray-400">
          {description}
        </p>
      )}
    </div>
  );
}