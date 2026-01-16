export default async function ApprovalPage(props: {
  params: Promise<{ projectId: string }>;
}) {
  const params = await props.params;
  console.log("params", params);
  return <div>Approval Page {params?.projectId}</div>;
}
