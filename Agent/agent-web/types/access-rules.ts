export type RuleColumns = {
  description: string;
  inputUser: string;
  inputProject: string;
  outputTag: string;
  outputValue: string;
  outputEnv: string;
}

export type RuleAction = "edit" | "delete" | "save";

export const DUMMY_RULES: RuleColumns[] = [
  {
    description: "-",
    inputUser: "Any",
    inputProject: "Any",
    outputTag: "trinoUsername",
    outputValue: 'string(project) + "-user-" + string(user)',
    outputEnv: "trino",
  },
  {
    description: "-",
    inputUser: "Empty",
    inputProject: "Empty",
    outputTag: "submissionId",
    outputValue: "string(submissionId)",
    outputEnv: "trino",
  },
  {
    description: "-",
    inputUser: "Empty",
    inputProject: "Empty",
    outputTag: "trinoPassword",
    outputValue: '""',
    outputEnv: "trino",
  },
  {
    description: "-",
    inputUser: "Empty",
    inputProject: "Empty",
    outputTag: "trinoURL",
    outputValue: "http://localhost",
    outputEnv: "trino",
  },
  {
    description: "-",
    inputUser: "Any",
    inputProject: "Any",
    outputTag: "postgresUsername",
    outputValue: 'string(project) + "-user-" + string(user)',
    outputEnv: "postgres",
  },
  {
    description: "-",
    inputUser: "Any",
    inputProject: "Any",
    outputTag: "postgresPassword",
    outputValue: '""',
    outputEnv: "postgres",
  },
  {
    description: "-",
    inputUser: "Empty",
    inputProject: "Empty",
    outputTag: "postgresDatabase",
    outputValue: "tredata",
    outputEnv: "postgres",
  },
  {
    description: "-",
    inputUser: "Empty",
    inputProject: "Empty",
    outputTag: "postgresServer",
    outputValue: "10.8.0.4",
    outputEnv: "postgres",
  },
  {
    description: "-",
    inputUser: "Empty",
    inputProject: "Empty",
    outputTag: "postgresPort",
    outputValue: "5432",
    outputEnv: "postgres",
  },
  {
    description: "-",
    inputUser: "Empty",
    inputProject: "Empty",
    outputTag: "postgresSchema",
    outputValue: "public",
    outputEnv: "postgres",
  },
  {
    description: "-",
    inputUser: "Empty",
    inputProject: "Empty",
    outputTag: "submissionId",
    outputValue: "string(submissionId)",
    outputEnv: "postgres",
  },
];