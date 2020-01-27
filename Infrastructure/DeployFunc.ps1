## AZURE FUNCTION

#1.Install dotnet cli templates

#https://github.com/Azure/azure-functions-templates/wiki/Using-the-templates-directly-via-dotnet-new
#https://www.nuget.org/api/v2/package/Microsoft.Azure.WebJobs.ItemTemplates/2.0.10369,
#https://www.nuget.org/api/v2/package/Microsoft.Azure.WebJobs.ProjectTemplates/2.0.10369"

dotnet new -i "C:\Users\Marilag Dimatulac\Downloads\microsoft.azure.webjobs.projecttemplates.2.0.10369.nupkg"
dotnet new -i "C:\Users\Marilag Dimatulac\Downloads\microsoft.azure.webjobs.itemtemplates.2.0.10369.nupkg"

#2. Install function tool
npm install azure-functions-core-tools@core -g


#3. Create function
dotnet new "Azure Function" 
dotnet new HttpTrigger -n fn1
dotnet build

#code .
#func start --script-root bin\Debug\netcoreapp2.1
#curl http://localhost:7071/api/fn1

#4. Install packages
dotnet add package Microsoft.Azure.Cosmos --interactive
dotnet add package Microsoft.Extensions.Configuration.AzureKeyVault --interactive
#<PackageReference Include="Microsoft.Extensions.Configuration.AzureKeyVault" Version="2.1.1" />
dotnet add package  Microsoft.Azure.Cosmos
## Deploy

#3.1 Create with enable msi 
az storage account  create -n storcosmosoptimize -g rg-cosmos-optimize -l westeurope --sku Standard_LRS
az functionapp create --name func-cosmos-optimize-weu --consumption-plan-location westeurope -g rg-cosmos-optimize -s storcosmosoptimize

#3.2. Add function MSI access to keyvault
#3.3 Deploy code