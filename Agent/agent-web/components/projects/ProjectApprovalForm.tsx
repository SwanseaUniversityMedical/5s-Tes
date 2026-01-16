"use client";

import { Formik, Form, Field } from "formik";

type ApprovalFormValues = {
  submissionProjectName: string;
  description: string;
  decision: number;
  approvedBy: string;
  lastDecisionDate: string;
  projectExpiryDate: string;
  submissionBucketTre: string;
  outputBucketTre: string;
  outputBucketSub: string;
  archived: boolean;
};

function formatDate(value?: string) {
  if (!value) return "";
  return new Date(value).toLocaleString();
}

export function ProjectApprovalForm({ project }: { project: any }) {
  const initialValues: ApprovalFormValues = {
    submissionProjectName: project.submissionProjectName ?? "",
    description: project.description ?? "",
    decision: project.decision,
    approvedBy: project.approvedBy ?? "",
    lastDecisionDate: formatDate(project.lastDecisionDate),
    projectExpiryDate: formatDate(project.projectExpiryDate),
    submissionBucketTre: project.submissionBucketTre ?? "",
    outputBucketTre: project.outputBucketTre ?? "",
    outputBucketSub: project.outputBucketSub ?? "",
    archived: project.archived,
  };

  return (
    <Formik initialValues={initialValues} onSubmit={() => {}}>
      <Form className="space-y-8">
        {/* Project Details */}
        <section className="rounded-lg border p-6">
          <h2 className="mb-4 text-lg font-semibold">Project Details</h2>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <label className="block text-sm font-medium">Project Name</label>
              <Field
                name="submissionProjectName"
                disabled
                className="mt-1 w-full rounded border bg-gray-100 p-2"
              />
            </div>

            <div>
              <label className="block text-sm font-medium">Decision</label>
              <Field
                name="decision"
                disabled
                className="mt-1 w-full rounded border bg-gray-100 p-2"
              />
            </div>

            <div className="md:col-span-2">
              <label className="block text-sm font-medium">Description</label>
              <Field
                as="textarea"
                name="description"
                disabled
                rows={3}
                className="mt-1 w-full rounded border bg-gray-100 p-2"
              />
            </div>
          </div>
        </section>

        {/* Approval Info */}
        <section className="rounded-lg border p-6">
          <h2 className="mb-4 text-lg font-semibold">Approval Information</h2>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
            <div>
              <label className="block text-sm font-medium">Approved By</label>
              <Field
                name="approvedBy"
                disabled
                className="mt-1 w-full rounded border bg-gray-100 p-2"
              />
            </div>

            <div>
              <label className="block text-sm font-medium">
                Last Decision Date
              </label>
              <Field
                name="lastDecisionDate"
                disabled
                className="mt-1 w-full rounded border bg-gray-100 p-2"
              />
            </div>

            <div>
              <label className="block text-sm font-medium">
                Project Expiry Date
              </label>
              <Field
                name="projectExpiryDate"
                disabled
                className="mt-1 w-full rounded border bg-gray-100 p-2"
              />
            </div>
          </div>
        </section>

        {/* Storage Buckets */}
        <section className="rounded-lg border p-6">
          <h2 className="mb-4 text-lg font-semibold">Storage Buckets</h2>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
            <div>
              <label className="block text-sm font-medium">
                Submission Bucket (TRE)
              </label>
              <Field
                name="submissionBucketTre"
                disabled
                className="mt-1 w-full rounded border bg-gray-100 p-2"
              />
            </div>

            <div>
              <label className="block text-sm font-medium">
                Output Bucket (TRE)
              </label>
              <Field
                name="outputBucketTre"
                disabled
                className="mt-1 w-full rounded border bg-gray-100 p-2"
              />
            </div>

            <div>
              <label className="block text-sm font-medium">
                Output Bucket (Sub)
              </label>
              <Field
                name="outputBucketSub"
                disabled
                className="mt-1 w-full rounded border bg-gray-100 p-2"
              />
            </div>
          </div>
        </section>

        {/* Status */}
        <section className="rounded-lg border p-6">
          <h2 className="mb-4 text-lg font-semibold">Status</h2>

          <div className="flex items-center gap-2">
            <Field type="checkbox" name="archived" disabled />
            <span className="text-sm">Archived</span>
          </div>
        </section>
      </Form>
    </Formik>
  );
}
