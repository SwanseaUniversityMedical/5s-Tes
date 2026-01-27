import AccessRulesTable from "@/components/access-rules/AccessRulesTable";


export default function AccessRules() {
  return (
    <div className="pt-6 pb-8">

      <h1 className="text-2xl pb-3 font-bold pl-5">Access Rules</h1>
      <AccessRulesTable />
    </div>
  );
}