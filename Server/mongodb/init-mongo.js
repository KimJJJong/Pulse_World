const appDatabase = process.env.MONGO_APP_DATABASE;
const appUsername = process.env.MONGO_APP_USERNAME;
const appPassword = process.env.MONGO_APP_PASSWORD;

if (!appDatabase || !appUsername || !appPassword) {
    throw new Error("Mongo application user environment variables are required.");
}

const applicationDb = db.getSiblingDB(appDatabase);
if (!applicationDb.getUser(appUsername)) {
    applicationDb.createUser({
        user: appUsername,
        pwd: appPassword,
        roles: [{ role: "readWrite", db: appDatabase }]
    });
}

