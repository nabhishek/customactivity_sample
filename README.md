# Summary
### C# Sample for resolving AKV enabled Azure Data Factory's Linked Services JSON payload and fetching the actual secrets using custom code. Authenticating to AKV via a Certificate. Then deletes file/folder from the retrieved storage account.

### For running it on Azure Batch
1. In code, modify the linkedServices.json accordingly with your AKV URL/ Secret name
2. Create a certificate on local machine. Using PowerShell (admin): 
PS C:\WINDOWS\system32> New-SelfSignedCertificate -Subject "CN=akv.mycustomactivity.com" -CertStoreLocation "cert:\LocalMachine\My" -KeyLength 2048 -KeySpec Signature
3. Goto 'Manage Computer Certificates' in Control Panel, and locate the certificate under Personal -> Certificates. Export the private key -  use default options in Export File Format window, set a password, filename.
4. Export the public key - use default option, specify filename.
5. In Batch pool, assign the self-signed certificate (private key). We need to assign the certificate to the batch account, this will in turn allow us to assign it to the pool (VMs). The easiest way to do this, is a one off task through the portal, go to your batch account, then the “certificates” blade and click add. Upload the PFX file we generated earlier and supply the password. Once complete, this should show in the list of certificates and you can verify the thumbprint.
You can now create a batch pool or select an existing pool, and you should be able to find the above certificate under the certificate blade and then be able to assign the certificate you created to the pool. When you do so, make sure you select **Store Name** -> "My", **Store Loacation** -> “CurrentUser” for the store location. This cert will now be available for all the tasks running on the batch nodes.
6. Create a Service Principal and associate the certificate in [Azure Active Directory](https://docs.microsoft.com/en-us/azure/azure-resource-manager/resource-group-authenticate-service-principal#create-service-principal-with-self-signed-certificate). Use the self-signed certificate (public key) from setp 4.
7. Grant Rights to KeyVault to the Service Principal created in Step 6.
8. Run the code now 

### For running it on a local windows machine (debugging)
1. In code, modify the linkedServices.json accordingly with your AKV URL/ Secret name
2. Create a certificate on local machine. Using PowerShell (admin): 
PS C:\WINDOWS\system32> New-SelfSignedCertificate -Subject "CN=akv.mycustomactivity.com" -CertStoreLocation "cert:\LocalMachine\My" -KeyLength 2048 -KeySpec Signature
3. Goto 'Manage Computer Certificates' in Control Panel, and locate the certificate under Personal -> Certificates. Export the private key -  use default options in Export File Format window, set a password, filename.
4. Export the public key - use default option, specify filename.
5. Create a Service Principal and associate the certificate in [Azure Active Directory](https://docs.microsoft.com/en-us/azure/azure-resource-manager/resource-group-authenticate-service-principal#create-service-principal-with-self-signed-certificate). Use the self-signed certificate (public key) from setp 4.
6. Grant Rights to KeyVault to the Service Principal created in Step 5.
8. Run the code now on the local machine (where the certificate was generated)
