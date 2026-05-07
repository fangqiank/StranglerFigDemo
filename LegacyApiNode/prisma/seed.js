import { PrismaClient } from "@prisma/client";
import { PrismaBetterSqlite3 } from "@prisma/adapter-better-sqlite3";
import { URL } from "node:url";
import { dirname, join } from "node:path";

const __dirname = dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Z]:)/, "$1"));
const dbPath = join(__dirname, "..", "..", "shared.db");

const adapter = new PrismaBetterSqlite3({ url: `file:${dbPath}` });
const prisma = new PrismaClient({ adapter });

async function main() {
  const count = await prisma.user.count();
  if (count > 0) {
    console.log("Database already seeded, skipping...");
    return;
  }

  await prisma.user.createMany({
    data: [
      { user_name: "Alice Johnson", user_email: "alice@legacy.com", created_date: "2023-01-15", status_code: "A" },
      { user_name: "Bob Smith", user_email: "bob@legacy.com", created_date: "2023-02-20", status_code: "A" },
      { user_name: "Charlie Brown", user_email: "charlie@legacy.com", created_date: "2023-03-10", status_code: "I" },
      { user_name: "Diana Prince", user_email: "diana@legacy.com", created_date: "2023-04-05", status_code: "A" },
      { user_name: "Eve Wilson", user_email: "eve@legacy.com", created_date: "2023-05-12", status_code: "A" },
      { user_name: "Frank Castle", user_email: "frank@legacy.com", created_date: "2023-06-20", status_code: "A" },
      { user_name: "Grace Hopper", user_email: "grace@legacy.com", created_date: "2023-07-15", status_code: "I" },
    ],
  });
  console.log("Seed data inserted.");
}

main()
  .catch(console.error)
  .finally(() => prisma.$disconnect());
