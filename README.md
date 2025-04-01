# Azure PR Agent Automation

Automatically runs a Docker container in Azure Container Instances (ACI) when triggered by Azure DevOps pull request webhooks.

<img src="https://github.com/user-attachments/assets/447c655b-4798-4605-bfb0-e5fcb7bf3477" width="700">




## Features
- 🚀 Automatic container execution on PR creation
- 🔒 Managed Identity authentication
- 🧹 Automated cleanup of completed containers
- 🔄 Full integration with Azure DevOps

## Prerequisites
- Azure account with contributor permissions
- Azure Resource Group with contributor permission
- Azure Container Registry (ACR) with pullRole permission
- Azure Function App
- Docker image pushed to ACR
- Azure DevOps project with admin access

## Setup Guide

### 1. Create a resource group

##### **Using Azure Portal**
  - Azure Portal > Resource Groups > Create
<table>
  <tr>
    <td><img src="https://github.com/user-attachments/assets/cd279a40-7689-4b77-b31e-4d24bf287fcd" alt="image" width="450"></td>
    <td><img src="https://github.com/user-attachments/assets/44b07a00-f5dd-4380-b0c7-907f8122490d" alt="image" width="450"></td>
  </tr>
</table>


##### **Using CLI**
  - ```bash
    azure login
    ```
  - ```bash
    az group create --name <RESOURCE_GROUP_NAME> --location <REGION>
    ```

---
### 2. Prepare Azure Resources

#### Create Azure Container Registry (ACR) (**Use lowercase characters when naming the container registry**)

##### **Using Azure Portal**
- Azure Portal > Container Registries > Create
<table>
  <tr>
    <td><img width="959" alt="image (9)" src="https://github.com/user-attachments/assets/ff78e47a-ca3e-4d92-841e-d327b6170ac1" /></td>
    <td><img width="524" alt="image (10)" src="https://github.com/user-attachments/assets/6fbfe2b4-3111-48a7-b63d-cd65ccd88381" /></td>
  </tr>
</table>


##### **Using CLI**
```bash
az acr create --resource-group <RESOURCE_GROUP_NAME> --name <ACR_NAME> --sku Basic
```

#### Push Docker Image to ACR
1. Clone this repository
```bash
git clone https://github.com/99x-incubator/azure_pr_review_agent
```
2. Navigate to the folder
``` bash
cd azure_pr_review_agent
```
3. Open a command prompt and build the Docker Image
```bash
docker build -t azure-pr-agent .
```
4. Login to azure
```bash
az login
```
5. Login to the container registry
```bash
az acr login --name <ACR_NAME>
```
6. Tag the built image
```bash
docker tag azure-pr-agent <ACR_NAME>.azurecr.io/azure-pr-agent:latest
```
7. Push the image to the container registry
```bash
docker push <ACR_NAME>.azurecr.io/azure-pr-agent:latest
```
---
### 3. Configure Function App

#### Create Function App

##### **Using Azure Portal**
- Use .NET runtime stack
<table>
  <tr>
    <td><img width="600" alt="image" src="https://github.com/user-attachments/assets/02a60481-ac49-462a-af69-5462276eceb1" /></td>
    <td><img width="575" alt="image" src="https://github.com/user-attachments/assets/eb31fbcf-5d49-44b7-88be-c82f66c540eb" /></td>
  </tr>
</table>

- Enable Azure OpenAI when creating the function app
  
- <img src="https://github.com/user-attachments/assets/d260ff78-b243-456c-83a0-e23ba2980ded" alt="image" width="600">



##### **Using CLI**

```bash
az functionapp create --resource-group <RESOURCE_GROUP_NAME> --consumption-plan-location <REGION> --runtime dotnet-isolated --functions-version 4 --name <APP_NAME> --storage-account <STORAGE_NAME>
```

### 4. Configure Permissions(Either you must be an owner of the Azure account you are using, or you must contact an owner of that account and follow these steps)

#### Enable Managed Identity

##### **Using Azure Portal**
- Go to the function app
<table>
  <tr><td><img src="https://github.com/user-attachments/assets/4f56e89f-6d12-4d31-875a-0a4871c4516d" alt="image" width="800"></td></tr>
  <tr><td><img src="https://github.com/user-attachments/assets/615b61bc-94d5-4c0e-87dd-417fe7632073" alt="image" width="800"></td></tr>
  <tr><td><img src="https://github.com/user-attachments/assets/cf004570-02c4-4b25-9245-9ba3d73c4a58" alt="image" width="500"></td></tr>
</table>

##### **Using CLI**
```bash
# Get Function App's Managed Identity principal ID
az functionapp identity show --name <your-function-app-name> --resource-group <your-resource-group> --query principalId -o tsv
```
 ```bash
# Assign Contributor role to Managed Identity (replace with your actual subscription and RG)
az role assignment create --assignee <PRINCIPAL_ID(FROM_ABOVE_COMMAND)> --role "Contributor" --scope "/subscriptions/<SUBSCRIPTION_ID>/resourceGroups/<RESOURCE_GROUP_NAME>"
```

#### Assign ACR Pull Role
##### **Using Azure Portal**
<table>
  <tr><td><img width="800" alt="image (6)" src="https://github.com/user-attachments/assets/acfb362c-a7fd-4180-a569-bd1226650b7a" /></td></tr>
  <tr><td><img width="800" alt="image (7)" src="https://github.com/user-attachments/assets/f4d2696d-ff1d-4d4b-97e1-d0054d83003c" /></td></tr>
  <tr><td><img width="800" alt="image (13)" src="https://github.com/user-attachments/assets/9a33d020-98ae-41c4-8e5a-9b5a7f36b2f1" /></td></tr>
</table>




##### **Using CLI**
```bash
az role assignment create --assignee <PRINCIPAL_ID(FROM_PREVIOUS_COMMAND> --role AcrPull --scope /subscriptions/<SUB_ID>/resourceGroups/<RESOURCE_GROUP_NAME>/providers/Microsoft.ContainerRegistry/registries/<ACR_NAME>
```
---

### 5. Create Azure OpenAI and Deploy a Model
<table>
  <tr>
<td><img width="612" alt="image" src="https://github.com/user-attachments/assets/78bc5cf3-32ab-4884-91e2-5ab9f50be399" /></td>
<td><img width="556" alt="image" src="https://github.com/user-attachments/assets/7ef3554e-f52c-40c6-b07c-392dde93a10c" /></td>
    </tr>
  <tr>
<td><img width="731" alt="image" src="https://github.com/user-attachments/assets/eb8b3114-4ad5-40c9-88a6-34f51ce3f03f" /></td>
<td><img width="960" alt="image" src="https://github.com/user-attachments/assets/ad57f5aa-d9e5-434f-802d-b916f1d42598" /></td>
    </tr>
  <tr>
<td><img width="960" alt="image" src="https://github.com/user-attachments/assets/783a3822-fbc6-4aa1-94a9-53bbbb0cfae9" /></td>
<td><img width="960" alt="image" src="https://github.com/user-attachments/assets/300a0051-dd5f-44d6-9ae2-4bcf91c16496" /></td>
</tr>
</table>




### 5. Environment Variables (Inside the function app)
**Change AZURE_OPENAI_KEY to AZURE_OPENAI_API_KEY in environmental variables**
- <img width="600" alt="image" src="https://github.com/user-attachments/assets/6fd6e632-73d7-46fb-9328-c87d8cf90266" />



- You can find the ACR_NAME, ACR_PASSWORD, ACR_USERNAME as follows 
- <img width="500" alt="image" src="https://github.com/user-attachments/assets/cb279d60-2be4-4c7b-87cc-4ee714d05781" />
- (Make sure that you have enabled admin user for the container registry)

---
### 6. Deploy Function Code
- #### Clone the Repository
    - Start by cloning the repository to your local machine:

```bash
git clone https://github.com/99x-incubator/azure_pr_review_agent_azure_func_net.git
cd azure_pr_review_agent_azure_func_net
```
- #### Deploy via Visual Studio Code
  - Open the Project: Open the cloned repository in VS Code.
  - Sign in to Azure: Make sure you are signed in to your Azure account using the Azure: Sign In command.
  - Deploy the Function App: Press ```Ctrl```+```Shift```+```P``` to open the Command Palette.
  - Type and select **Azure Functions: Deploy to Function App**.
  - Choose your target Function App from the list.
  - Confirm the deployment when prompted and wait for the process to complete.
- #### Deploy via Terminal
    - Alternatively, you can deploy the function using the Azure Functions Core Tools:
    - Ensure prerequisites are met: Make sure you have Azure Functions Core Tools installed and you are logged in via the Azure CLI.
    - Deploy using the CLI:
    ```bash
    # Log in to Azure if you haven't already
    az login
    ```
    ```bash
    # Publish your function to the specified Function App
    func azure functionapp publish <FUNCTION_APP_NAME>
    ```
- After deployment, the functions should appear in the function app as follows in the portal
- <img width="953" alt="image (12)" src="https://github.com/user-attachments/assets/0bf96886-619a-4a93-b9f6-ee617955779b" />
**Note that the container cleanup function runs every 6 hours to remove container instances that are on the terminated, failed or successful state**

### 7. Configure Azure DevOps Webhook

1. Go to **Project Settings > Service Hooks**
2. Create new webhook with:
   - **Trigger**: Pull request created
   - **URL**: 
     - Using VSCode: ```ctrl```+```shift```+```P``` -> Azure Functions: Copy Function URL
     - Using CLI: Replace "==" in the end of the URL with "%3D%3D" after copying it before pasting in the webhook
       ```bash
       FOR /F "delims=" %a IN ('az functionapp function show --resource-group <RESOURCE_GROUP> --name <FUNCTION_APP> --function-name PRReviewTrigger --query invokeUrlTemplate -o tsv') DO SET "URL=%a"
        FOR /F "delims=" %b IN ('az functionapp function keys list --resource-group <RESOURCE_GROUP> --name <FUNCTION_APP> --function-name PRReviewTrigger --query default -o tsv') DO SET "KEY=%b"
        ECHO %URL%?code=%KEY%
       ```
<table>
  <tr>
    <td><img src="https://github.com/user-attachments/assets/61462fb2-cbe8-4b35-a83e-c1fd4cf0f915" alt="Image 1" width="400px" /></td>
    <td><img src="https://github.com/user-attachments/assets/d9669452-ae9d-479d-9234-c9443f3e6604" alt="Image 2" width="400px" /></td>
  </tr>
  <tr>
    <td><img src="https://github.com/user-attachments/assets/424b5a53-8d27-490e-8db4-c52867377b14" alt="Image 3" width="400px" /></td>
    <td><img src="https://github.com/user-attachments/assets/f25bd7d0-dd43-4a7d-b807-f10e58d56eab" alt="Image 4" width="400px" /></td>
  </tr>
  <tr>
    <td><img src="https://github.com/user-attachments/assets/d818d527-ecf1-4879-8c87-5cce28bba82e" alt="Image 5" width="400px" /></td>
    <td><img src="https://github.com/user-attachments/assets/a47945f8-c4ef-4400-a5cf-92e758e62f00" alt="Image 6" width="400px" /></td>
  </tr>
</table>

## Cleanup Automation
The container cleanup function will also be deployed to the function app at the deployement of the function mentioned before. This container cleanup function is a time trigger function and it'll delete the container instances that are being teminated, succeeded or failed.

