import { PrismaClient } from "@prisma/client";
import { PrismaBetterSqlite3 } from "@prisma/adapter-better-sqlite3";
import { URL } from "node:url";
import { dirname, join } from "node:path";

const __dirname = dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Z]:)/, "$1"));
const dbPath = join(__dirname, "..", "..", "shared.db");

const adapter = new PrismaBetterSqlite3({ url: `file:${dbPath}` });
const prisma = new PrismaClient({ adapter });
export default prisma;
