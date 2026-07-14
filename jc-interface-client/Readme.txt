How to generate clients:

Download the latest NSwagStudio: https://github.com/RicoSuter/NSwag/wiki/NSwagStudio

1.*** MAKE SURE YOU REMOVE THE baseUri from the RAML file ***
  *** MAKE SURE YOU REMOVE THE baseUri from the RAML file ***
  *** MAKE SURE YOU REMOVE THE baseUri from the RAML file ***
2. Convert RAML files from JC-Interface into OpenAPI (OAS3.0):  https://mulesoft.github.io/oas-raml-converter/ or use https://github.com/mulesoft/oas-raml-converter
   If using oas-raml-converter, give it some time, it may look like it's frozen but it will work. 
3. Update the *-oas3.json, *.raml files inside of the JC-Interface-client project.
4. Open up NSwagStudio
5. Load the NSwagConfig/*.nswag configurations
6. Paste in the updated OpenAPI (OAS3.0) specification JSON from step 2
7. Press Generate Outputs
8. Goto CSharp Client tab
9. Copy output into appropriate .cs file under Clients/
10. Repeat for each client
11. Run tests