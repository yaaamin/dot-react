-- Run this once against your MySQL database (e.g. in MySQL Workbench or `mysql < file.sql`).

CREATE TABLE IF NOT EXISTS audit_logs (
    id             CHAR(36)     NOT NULL,
    actor_type     VARCHAR(50)  NULL,
    actor_id       VARCHAR(100) NULL,
    action         VARCHAR(150) NULL,
    correlation_id VARCHAR(100) NULL,
    occurred_at    DATETIME(6)  NOT NULL,
    target_type    VARCHAR(100) NULL,
    target_id      VARCHAR(100) NULL,
    created_at     DATETIME(6)  NOT NULL,
    created_by     VARCHAR(100) NULL,
    PRIMARY KEY (id),
    -- Indexes for the questions you will actually ask of an audit log:
    INDEX ix_audit_logs_occurred_at (occurred_at),
    INDEX ix_audit_logs_actor       (actor_type, actor_id),
    INDEX ix_audit_logs_target      (target_type, target_id),
    INDEX ix_audit_logs_correlation (correlation_id),
    INDEX ix_audit_logs_action      (action)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

-- The ONLY way the app writes to the table. Note there is no update or delete procedure:
-- an audit log should be append-only.
DROP PROCEDURE IF EXISTS sp_audit_log_create;

DELIMITER //

CREATE PROCEDURE sp_audit_log_create (
    IN p_id             CHAR(36),
    IN p_actor_type     VARCHAR(50),
    IN p_actor_id       VARCHAR(100),
    IN p_action         VARCHAR(150),
    IN p_correlation_id VARCHAR(100),
    IN p_occurred_at    DATETIME(6),
    IN p_target_type    VARCHAR(100),
    IN p_target_id      VARCHAR(100),
    IN p_created_at     DATETIME(6),
    IN p_created_by     VARCHAR(100)
)
BEGIN
    INSERT INTO audit_logs
        (id, actor_type, actor_id, action, correlation_id, occurred_at,
         target_type, target_id, created_at, created_by)
    VALUES
        (p_id, p_actor_type, p_actor_id, p_action, p_correlation_id, p_occurred_at,
         p_target_type, p_target_id, p_created_at, p_created_by);
END //

DELIMITER ;

-- Optional hardening: give the app's MySQL user permission to run the procedure and nothing else
-- on this table, so even a bug or SQL injection elsewhere cannot rewrite history.
-- GRANT EXECUTE ON PROCEDURE your_db.sp_audit_log_create TO 'app_user'@'%';
