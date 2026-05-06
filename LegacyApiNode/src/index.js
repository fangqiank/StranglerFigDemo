const express = require("express");
const users = require("./data");

const app = express();
app.use(express.json());

// Response headers matching .NET LegacyApi
app.use((_req, res, next) => {
  res.set("X-API-Source", "legacy");
  res.set("X-API-Version", "2.3.1");
  res.set("X-System-Type", "monolithic-legacy");
  res.set("X-Served-By", "Node.js/Express (LegacyApi)");
  next();
});

// Health
app.get("/health", (_req, res) => {
  res.json({
    status: "healthy",
    system: "legacy",
    version: "2.3.1",
    uptime: "127 days",
  });
});

// Get all users
app.get("/api/users", (req, res) => {
  let result = [...users.values()];

  const { status, page = 1, limit = 10 } = req.query;
  if (status) {
    result = result.filter((u) => u.status_code.toLowerCase() === status.toLowerCase());
  }

  const currentPage = Number(page);
  const pageSize = Number(limit);
  const totalUsers = result.length;
  const paged = result.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  res.json({
    success: true,
    message: "Users retrieved successfully",
    data: paged.map((u) => ({
      id: u.user_id,
      name: u.user_name,
      email: u.user_email,
      created: u.created_date,
      active: u.status_code === "A",
    })),
    pagination: {
      current_page: currentPage,
      per_page: pageSize,
      total_items: totalUsers,
      total_pages: Math.ceil(totalUsers / pageSize),
    },
    source: "legacy-system",
  });
});

// Get user by ID
app.get("/api/users/:user_id", (req, res) => {
  const id = Number(req.params.user_id);
  const user = users.get(id);

  if (!user) {
    return res.status(404).json({
      success: false,
      error: "User not found",
      error_code: "USER_404",
      source: "legacy-system",
    });
  }

  res.json({
    success: true,
    data: {
      id: user.user_id,
      name: user.user_name,
      email: user.user_email,
      created: user.created_date,
      active: user.status_code === "A",
    },
    source: "legacy-system",
  });
});

// Create user (placeholder)
app.post("/api/users", (_req, res) => {
  res.json({
    success: true,
    message: "This endpoint is complex in legacy system",
    note: "Requires form parsing, validation middleware, etc.",
    source: "legacy-system",
  });
});

// Create user (actual)
app.post("/api/users/create", (req, res) => {
  const newId = Math.max(...users.keys()) + 1;
  const newUser = {
    user_id: newId,
    user_name: req.body.name,
    user_email: req.body.email,
    created_date: new Date().toISOString().slice(0, 10),
    status_code: "A",
  };
  users.set(newId, newUser);

  res.status(201).json({
    success: true,
    message: "User created",
    data: { id: newUser.user_id, name: newUser.user_name, email: newUser.user_email },
    source: "legacy-system",
  });
});

// Update user
app.put("/api/users/:user_id", (req, res) => {
  const id = Number(req.params.user_id);
  const user = users.get(id);
  if (!user) {
    return res.status(404).json({ success: false, error: "User not found", source: "legacy-system" });
  }

  if (req.body.name) user.user_name = req.body.name;
  if (req.body.email) user.user_email = req.body.email;
  if (req.body.status) user.status_code = req.body.status;

  res.json({ success: true, message: "User updated", source: "legacy-system" });
});

// Delete user
app.delete("/api/users/:user_id", (req, res) => {
  const id = Number(req.params.user_id);
  if (!users.delete(id)) {
    return res.status(404).json({ success: false, error: "User not found", source: "legacy-system" });
  }
  res.json({ success: true, message: "User deleted", source: "legacy-system" });
});

// Orders (stub)
app.get("/api/orders", (_req, res) => {
  res.json({ message: "Orders from legacy system", source: "legacy-system" });
});

// Products (stub)
app.get("/api/products", (_req, res) => {
  res.json({ message: "Products from legacy system", source: "legacy-system" });
});

const PORT = 5001;
app.listen(PORT, () => {
  console.log(`LegacyApi (Node.js) listening on http://localhost:${PORT}`);
});
