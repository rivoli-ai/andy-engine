# File System Test Coverage Gaps

## Current Status
We have basic "happy path" scenarios but are missing critical edge cases that the andy-tools tests cover.

## Coverage Analysis

### CopyFile Tool
**Current Coverage (3 scenarios):**
- ✅ Basic file copy
- ✅ Copy with overwrite enabled
- ✅ Recursive directory copy

**Missing Critical Edge Cases (16+ scenarios):**
- ❌ Overwrite disabled (should fail when destination exists)
- ❌ Copy to existing directory (should preserve filename)
- ❌ Copy with trailing directory separator
- ❌ Preserve timestamps
- ❌ Copy empty directory
- ❌ Exclude patterns (*.tmp, *.bak, *.log)
- ❌ Exclude directories (.git, node_modules)
- ❌ **Source file not found** (error handling)
- ❌ **Invalid/empty source path** (error handling)
- ❌ **Invalid/empty destination path** (error handling)
- ❌ **Missing required parameters** (error handling)
- ❌ **Path traversal security** (../..)
- ❌ Cancellation support
- ❌ Statistics validation (files_copied, bytes_copied)

### DeleteFile Tool
**Current Coverage (2 scenarios):**
- ✅ Basic file deletion
- ✅ Recursive directory deletion

**Missing Critical Edge Cases (13+ scenarios):**
- ❌ Delete with backup creation
- ❌ Delete read-only file WITH force flag
- ❌ Delete read-only file WITHOUT force (should fail)
- ❌ Delete non-empty directory WITHOUT recursive (should fail)
- ❌ Delete with size limits
- ❌ Delete with exclusion patterns
- ❌ **File not found** (error handling)
- ❌ **Invalid/empty path** (error handling)
- ❌ **Missing required parameters** (error handling)
- ❌ Delete empty directory
- ❌ Backup to default location
- ❌ Cancellation support
- ❌ Statistics validation (files_deleted, bytes_freed)

### MoveFile Tool
**Current Coverage (2 scenarios):**
- ✅ Basic file move
- ✅ Move with overwrite enabled

**Missing Critical Edge Cases (18+ scenarios):**
- ❌ Overwrite disabled (should fail when destination exists)
- ❌ Move to existing directory (should preserve filename)
- ❌ Move with trailing directory separator
- ❌ Move directory (not just files)
- ❌ Rename file (same directory)
- ❌ **Source file not found** (error handling)
- ❌ **Invalid/empty source path** (error handling)
- ❌ **Invalid/empty destination path** (error handling)
- ❌ **Missing required parameters** (error handling)
- ❌ **Path traversal security** (../..)
- ❌ Cross-device move (falls back to copy+delete)
- ❌ Cancellation support
- ❌ Statistics validation

### ReadFile Tool
**Current Coverage (2 scenarios):**
- ✅ Basic text file read
- ✅ JSON file read

**Missing Critical Edge Cases (5+ scenarios):**
- ❌ Read with max size limit (reject too large files)
- ❌ Read with different encodings
- ❌ **File not found** (error handling)
- ❌ **Path outside allowed directory** (security)
- ❌ Read binary file as text
- ❌ Read specific line range

### WriteFile Tool
**Current Coverage (2 scenarios):**
- ✅ Basic file write
- ✅ Append mode

**Missing Critical Edge Cases (6+ scenarios):**
- ❌ Write with overwrite disabled (should fail if exists)
- ❌ Create parent directories automatically
- ❌ Write with specific encoding
- ❌ **File path invalid/empty** (error handling)
- ❌ **Path outside allowed directory** (security)
- ❌ Write with size limits
- ❌ Write to read-only location (should fail)

## Priority Recommendations

### High Priority (Security & Error Handling)
These are critical for production use:

1. **Error Handling**: File not found, invalid paths, missing parameters
2. **Security**: Path traversal prevention (../..), path outside allowed directory
3. **Overwrite Protection**: Scenarios where operations should fail safely

### Medium Priority (Common Use Cases)
These cover common real-world scenarios:

1. Copy/move to directory (preserve filename)
2. Exclude patterns (.git, node_modules, *.log)
3. Read-only file operations (with/without force)
4. Backup before delete

### Lower Priority (Advanced Features)
Nice to have but less critical:

1. Cancellation support
2. Statistics validation
3. Timestamp preservation
4. Different encodings
5. Line range reading

## Recommended Action Plan

### Phase 1: Add Error Handling Scenarios (High Priority)
For each tool, add scenarios for:
- File/directory not found
- Invalid/empty paths
- Path traversal security
- Missing required parameters

### Phase 2: Add Safety Scenarios (High Priority)
- Overwrite disabled (should fail)
- Delete non-empty without recursive (should fail)
- Write to read-only location (should fail)
- Path outside working directory (should fail)

### Phase 3: Add Common Use Case Scenarios (Medium Priority)
- Copy/move to directory
- Exclude patterns
- Read-only file handling
- Backup before delete

### Phase 4: Add Advanced Scenarios (Lower Priority)
- Encoding options
- Size limits
- Line ranges
- Cancellation
- Statistics

## Summary

**Current:** 11 scenarios covering happy paths
**Needed:** 58+ additional scenarios for comprehensive coverage

**Recommendation:** Start with Phase 1 & 2 (error handling + safety) as these are critical for production use and will significantly improve test robustness.
