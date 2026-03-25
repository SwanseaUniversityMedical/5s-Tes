package play
import rego.v1
import input

default allow := false

allow if {
   print("input", input)
   allowed_test
}

allowed_test if input.action.operation in ["ExecuteQuery", "AccessCatalog"]
allowed_test if input.context.identity.user == input.action.resource.table.schemaName