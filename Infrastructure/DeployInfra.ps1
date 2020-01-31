## SETUP INFRASTRUCTURE
az login

az account set --subscription '6ce12045-b892-4728-86ef-91a982d596ac'
#Create resource group
az group create --name rg-cosmos-optimize --location westeurope
#Create keyvault
az keyvault create -n kv-cosmos -g rg-cosmos-optimize
#Create CosmosDB account
az cosmosdb create -g rg-cosmos-optimize --name db-cosmos-optmize
#Save primary key to keyvault
$key = az cosmosdb keys list -g cosmos-optimize -n db-cosmos-optimize --query primaryMasterKey
az keyvault secret set --vault-name kv-cosmos  -n cosmoskey --value $key
#Assign yourself permission on kv to run local
az keyvault set-policy -n kv-cosmos --upn md@dewise.com --secret-permissions get list 
--key-permissions --certificate-permissions --storage-permissions
