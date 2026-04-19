CREATE TABLE IF NOT EXISTS scheduled_demo_runs (
  id INT AUTO_INCREMENT PRIMARY KEY,
  note VARCHAR(100) NOT NULL,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO scheduled_demo_runs (note) VALUES ('run from scheduler');
