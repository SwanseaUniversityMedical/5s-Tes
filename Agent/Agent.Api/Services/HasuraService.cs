using System.Net.Http.Headers;
using System.Text;
using Agent.Api.Models;
using Agent.Api.Repositories;
using Agent.Api.Repositories.DbContexts;
using Newtonsoft.Json;
using Serilog;

namespace Agent.Api.Services
{

    public interface IHasuraService
    {
        public Task Run();   
        public Task<string> ExecuteQuery(string Token, string Query);
    }

    public class HasuraService : IHasuraService
    {
        public readonly HasuraSettings _hasuraSettings;
        private readonly ApplicationDbContext _applicationDbContext;
        private readonly IHasuraAuthenticationService _ihasuraAuthenticationService;

        public HasuraService(IHasuraAuthenticationService iHasuraAuthenticationService, HasuraSettings HasuraSettings, ApplicationDbContext applicationDbContext)
        {
            _hasuraSettings = HasuraSettings;
            _applicationDbContext = applicationDbContext;
            _ihasuraAuthenticationService = iHasuraAuthenticationService;
        }

        public async Task Run()
        {
            try
            {
                string dbName = _hasuraSettings.DbName;

                await SetUpDb(dbName, _hasuraSettings.EnvironmentVariableForDB);
                var Schemas = await this.Schemas(dbName);

                if (Schemas.Any())
                {

                    foreach (var schema in Schemas)
                    {

                        var data = await TablesInSchemas(dbName, schema[0]);
                        var tables = data.Where(x => x[0] != "table_name");
                        foreach (var table in tables)
                        {
                            var successful = await TrackData(dbName, schema[0], table[0]);
                            if (successful)
                            {
                                await SetPermission(dbName, schema[0], table[0]);
                            }
                        }
                    }
                }

                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        public class ReturnData
        {
            public string result_type { get; set; }
            public List<List<string>> result { get; set; }
        }

        public async Task SetUpDb(string DbName, string EnvironmentVariable)
        {
            // Set the endpoint URL
            string endpointUrl = _hasuraSettings.HasuraURL + "/v1/metadata";
            /*
            // Create the JSON payload
            string payload = @"
    {
      ""type"": ""pg_add_source"",
      ""args"": {
        ""name"": ""pg1"",
        ""configuration"": {
          ""connection_info"": {
            ""database_url"": {
               ""from_env"": ""POSTGRSS_LOGIN""
             },
            ""pool_settings"": {
              ""max_connections"": 50,
              ""idle_timeout"": 180,
              ""retries"": 1,
              ""pool_timeout"": 360,
              ""connection_lifetime"": 600
            },
            ""use_prepared_statements"": true,
            ""isolation_level"": ""read-committed""
          },
          ""connection_template"": {
            ""template"": ""{{ if $.request.session?[\""x-hasura-role\""] == \""admin\"" }} {{$.primary}} {{else}} {{$.connection_set.db_1}} {{ end }}""
          },
          ""connection_set"": [
            {
              ""name"": ""db_1"",
              ""connection_info"": {
                ""database_url"": {
                  ""from_env"": ""POSTGRSS_LOGIN""
                }
              }
            }
          ]
        },
        ""replace_configuration"": false,
        ""customization"": {
          ""root_fields"": {
            ""namespace"": ""some_field_name"",
            ""prefix"": ""some_field_name_prefix"",
            ""suffix"": ""some_field_name_suffix""
          },
          ""type_names"": {
            ""prefix"": ""some_type_name_prefix"",
            ""suffix"": ""some_type_name_suffix""
          },
          ""naming_convention"": ""hasura-default""
        }
      }
    }";*/


            string payload = @"
{
  ""type"": ""pg_add_source"",
  ""args"": {
    ""name"": """ + DbName + @""",
    ""configuration"": {
      ""connection_info"": {
        ""database_url"": {
           ""from_env"": """ + EnvironmentVariable + @"""
         }
      },
      ""connection_set"": [
        {
          ""name"": """ + DbName + @""",
          ""connection_info"": {
            ""database_url"": {
              ""from_env"": """ + EnvironmentVariable + @"""
            }
          }
        }
      ]
    }
  }
}";

            try
            {
                var data = await HttpClient(endpointUrl, payload);
                var strign = await data.Content.ReadAsStringAsync();
                Log.Information($"TablesInSchemas ReadAsStringAsync " + strign);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);

            }
        }


        public async Task<List<List<string>>> TablesInSchemas(string DbName, string Schema)
        {

            // Set the endpoint URL
            string endpointUrl = _hasuraSettings.HasuraURL + "/v2/query";

            // Create the JSON payload
            string payload = @"
{
   ""type"": ""run_sql"",
   ""args"": {
        ""source"": """ + DbName + @""",
        ""sql"": ""SELECT table_name FROM information_schema.tables WHERE table_schema = '" + Schema + @"'""
    }
}";
            try
            {
                var Result = await HttpClient(endpointUrl, payload);

                var strign = await Result.Content.ReadAsStringAsync();
                Log.Information($"TablesInSchemas ReadAsStringAsync " + strign);
                var data = JsonConvert.DeserializeObject<ReturnData>(strign);

                return data.result;

            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);

            }

            return new List<List<string>>();

        }

        public async Task<List<List<string>>> Schemas(string DbName)
        {

            // Set the endpoint URL
            string endpointUrl = _hasuraSettings.HasuraURL + "/v2/query";

            // Create the JSON payload
            string payload = @"
{
   ""type"": ""run_sql"",
   ""args"": {
        ""source"": """ + DbName + @""",
        ""sql"": ""SELECT schema_name FROM information_schema.schemata;""
    }
}";

            try
            {
                var Result = await HttpClient(endpointUrl, payload);

                var Content = await Result.Content.ReadAsStringAsync();

                Log.Information("TablesInSchemas ReadAsStringAsync > "  + Content);

                var data = JsonConvert.DeserializeObject<ReturnData>(Content);

                return data.result;

            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);

            }

            return new List<List<string>>();
        }

        public async Task<bool> TrackData(string Db, string Schema, string table)
        {
            // Set the endpoint URL
            string endpointUrl = _hasuraSettings.HasuraURL + "/v1/metadata";
            /*
            // Create the JSON payload
            string payload = @"
    {
      ""type"": ""pg_track_table"",
      ""args"": {
        ""source"": ""pg1"",
        ""table"": ""two"",
        ""configuration"": {
          ""custom_root_fields"": {
            ""select"": ""Authors"",
            ""select_by_pk"": ""Author"",
            ""select_aggregate"": ""AuthorAggregate"",
            ""insert"": ""AddAuthors"",
            ""insert_one"":""AddAuthor"",
            ""update"": ""UpdateAuthors"",
            ""update_by_pk"": ""UpdateAuthor"",
            ""delete"": ""DeleteAuthors"",
            ""delete_by_pk"": ""DeleteAuthor""
          },
          ""column_config"": {
            ""id"": {
              ""custom_name"": ""authorId"",
              ""comment"": ""The ID of the Author""
            }
          },
          ""comment"": ""Authors of books""
        },
        ""apollo_federation_config"": {
          ""enable"": ""v1""
        }
      }
    }";
            */

            string payload = @"
{
  ""type"": ""pg_track_table"",
  ""args"": {
    ""source"": """ + Db + @""",
    ""table"":{
      ""schema"": """ + Schema + @""",
      ""name"": """ + table + @"""
    },
    ""configuration"": {
      ""custom_root_fields"": {
      },
      ""column_config"": {
      }
    },
    ""apollo_federation_config"": {
      ""enable"": ""v1""
    }
  }
}";

            try
            {
                var data = await HttpClient(endpointUrl, payload, true);
                if (data.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    var error = await data.Content.ReadAsStringAsync();
                    
                    if (error.Contains("already-tracked"))
                    {
                        if (_applicationDbContext.GeneratedRole.Any(x => x.RoleName == Schema) == false)
                        {
                            _applicationDbContext.GeneratedRole.Add(new GeneratedRole()
                            {
                                RoleName = Schema,
                            });
                        } 
                    }
                   
                    Log.Information($"TrackData error > {error} table > {table} Schema > {Schema} Db > {Db} ");
                    return false;
                }

            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);

            }
            return false;

        }

        public async Task SetPermission(string Db, string Schema, string table)
        {
            // Set the endpoint URL
            string endpointUrl = _hasuraSettings.HasuraURL + "/v1/metadata";

            // Create the JSON payload
            string payload = @"
        {
          ""type"": ""pg_create_select_permission"",
          ""args"": {
            ""source"": """ + Db + @""",
            ""table"":{
              ""schema"": """ + Schema + @""",
              ""name"": """ + table + @"""
            },
            ""role"": """ + Schema + @""",
            ""permission"": {
              ""columns"": ""*"",
              ""filter"": {
              }
            }
          }
        }";
            if (_applicationDbContext.GeneratedRole.Any(x => x.RoleName == Schema) == false)
            {
                _applicationDbContext.GeneratedRole.Add(new GeneratedRole()
                {
                    RoleName = Schema,
                });
            }

            try
            {
                var data = await HttpClient(endpointUrl, payload);
                var strign = await data.Content.ReadAsStringAsync();
                Log.Information($"TablesInSchemas ReadAsStringAsync " + strign);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);

            }
        }

        public async Task<string> ExecuteQuery(string Token, string Query)
        {

            //eg query {   testHasura_testing {     id     name   } }
          

            //need to get the headers to send from the token userid
            string endpointUrl = _hasuraSettings.HasuraURL + "/v1/graphql";
            try
            {
                var Result = await HttpClient(endpointUrl, Query, false, Token);

                var Content = await Result.Content.ReadAsStringAsync();

                return Content.ToString();

            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);

            }

            return "";

        }


        public async Task<HttpResponseMessage> HttpClient(string endpointUrl, string payload, bool doto = false, string token = "")
        {
            HttpResponseMessage response = null;
            // Create the HttpClient
            using (HttpClient client = new HttpClient())
            {
                // Set the request headers
                HttpRequestMessage re = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
          
          
                if (token != "")
                {   
                    re.Headers.Add("token", token);
                    re.Content = new StringContent($"{{ \"query\": \"{payload}\" }}", Encoding.UTF8, "application/json");
                }
                else
                {
                    re.Headers.Add("x-hasura-admin-secret", _hasuraSettings.HasuraAdminSecret);
                    re.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                }
               
               
                re.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                // Send the POST request to add the role and permission
                response = await client.SendAsync(re);

              //What is doto John?
              if (response.IsSuccessStatusCode)
                {
                    if (doto)
                    {
                        Console.WriteLine("Role and permission added successfully!");
                    }
                    Console.WriteLine("Role and permission added successfully!");
                }
                else
                {
                    if (doto)
                    {
                        Console.WriteLine("Failed to add role and permission. Status code:");
                    }
                    Console.WriteLine("Failed to add role and permission. Status code: " + response.StatusCode + await response.Content.ReadAsStringAsync());
                }

            }
            return response;
        }

 

    }
}
