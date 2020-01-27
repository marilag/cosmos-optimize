## SETUP INFRASTRUCTURE
az login

az account list

az account set --subscription 6ce12045-b892-4728-86ef-91a982d596ac

az group create --name rg-cosmos-optimize --location westeurope

az keyvault create -n kv-cosmos -g rg-cosmos-optimize

az cosmosdb create -g rg-cosmos-optimize --name db-cosmos-optmize

$key = az cosmosdb keys list -g cosmos-optimize -n db-cosmos-optimize --query primaryMasterKey

az keyvault secret set --vault-name kv-cosmos  -n cosmoskey --value $key

#Assign yourself permission on kv to run local
az keyvault set-policy -n kv-cosmos --upn md@dewise.com --secret-permissions get list --key-permissions --certificate-permissions --storage-permissions

az cosmosdb sql database create -n sql-cosmos-optimize -a db-cosmos-optimize -g cosmos-optimize --throughput 1000

az cosmosdb sql container create -g cosmos-optimize -a db-cosmos-optimize -d sql-cosmos-optimize -n Mentor1 --partition-key-path "/MentorID"