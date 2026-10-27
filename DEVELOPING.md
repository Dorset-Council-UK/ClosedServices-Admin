## Dependencies
To run Closed Services Admin with minimal modification, you will need.
- A web server capable of running .NET applications, such as IIS, Kestrel or Azure (locally IIS Express or Kestrel should be fine). Linux servers can use Kestrel via an Nginx proxy.
- PostgreSQL 13+ with PostGIS extension

## Detailed setup instructions
First, clone the repository - the `main` branch should always be the latest, stable version.

### Setting up a database
To run the project locally you will need to create a suitable database. Postgres is the only database provider currently set up, but with minimal adjustments you could make it use any Entity Framework compatible provider. Install the PostGIS extension to enable geometries in Postgres. 

In your database, create a user for Entity Framework to use and give it permission to login. 

> You can either install the PostGIS extension now, or let Entity Framework do it for you. If you let Entity Framework do it for you, your user will need to (temporarily) have superuser permission in order to install extensions.

You can either let Entity Framework create your schema for you, or create it yourself. 
#### Create the schema yourself
Create a schema within your database called closedservices. To avoid complex permissions, make the user account you created the owner of the schema.

#### Let EF create the schema for you
In order for EF to be able to create the schema, your user will need permission on the database to create schemas.

#### Using a different schema name
If you want to use a different schema name, you can change the app settings `ClosedServices:Database:Schema`, however, if you generate an EF migration script, you will have to manually find and change the schema name in the generated SQL before running it against your database. You will also need to change the SearchPath in your connection string to include your schema.

### User secrets and connection strings
Once you’ve set up your database, add a user secret to your local copy of the application. In Visual Studio this is done by right-clicking on the web project and selecting “Manage user secrets”.

In user secrets add the below line of code and fill in your database name, username and password:

`{
"ConnectionStrings:closedservices-admin": "Host=localhost;Database=YourDatabaseName;Username=YourUserName;Password=YourPassword;SearchPath=closedservices,public"
}`

You should now be able to test your connection and run migrations using Entity Framework commands.

You can choose to use appSettings or Environment Variables to store your secrets if you prefer. Azure KeyVault is also available with a few configuration settings, and is recommended for production scenarios. appSettings is NOT recommended for storing secret information such as connection strings.

### Setting up authentication
Currently this app only supports Microsoft Entra External ID. Refer to the official documentation to set this up in your Azure instance.

Once you've got a service set up, you'll need to add a number of settings to your user secrets to enable it.

```
"ClosedServices": {
	"AzureAd": {
	  "Instance": "https://<your-instance-name>.b2clogin.com",
	  "Domain": "<your-b2c-domain>",
	  "ClientId": "<your-client-id>",
	  "TenantId": "<your-tenant-id>",
	  "ClientSecret": "<your-client-secret>",
      "ResponseType": "code"
	}
}
```

### Running the application
Once you’ve followed the steps above, you’re ready to run the application. 

This application uses [Aspire](https://aspire.dev). Follow the instructions in the Aspire documentation to ensure you can use Aspire.

Provided you have Docker or Podman installed, you should be able to just run the AppHost project and it will build and run the application. You can also run the application directly from Visual Studio or using the .NET CLI.

This will give you a minimal starting application, but there are a couple more steps to take before it will be usable.

### Giving yourself permission
When the app runs, you should be able to login, but you will be unable to do anything. You need to give yourself an appropriate role in the application before you'll be able to do anything. 

To give yourself full admin privileges (including managing other users), go into your Azure Enterprise Application and give yourself the role 'Admin'.

To just assign yourself to a service, go into the database, and insert a record into the `ServiceUsers` table with your user ID and the service you want to manage. You can find your user ID by clicking 'My account' in the top right of the app.
