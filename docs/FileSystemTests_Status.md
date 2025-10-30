# File System Integration Tests - Status Report

**Last Updated:** 2025-10-30
**Test Suite Version:** 1.0
**LLM Provider:** OpenAI (gpt-4o-mini)
**Total Tests:** 80 test methods (160 test cases including Mock mode)

This document tracks the status of all file system integration tests when run with Real LLM mode. Each test is documented with its purpose, current status, issues encountered, and recommendations for fixes.

---

## 📊 Summary Statistics

| Tool | Total Tests | ✅ Passing | ❌ Failing | Success Rate |
|------|-------------|-----------|-----------|--------------|
| **ListDirectory** | 11 | 7 | 4 | 64% |
| **CopyFile** | 17 | 0 | 17 | 0% |
| **DeleteFile** | 15 | 6 | 9 | 40% |
| **MoveFile** | 18 | 2 | 16 | 11% |
| **ReadFile** | 9 | 1 | 8 | 11% |
| **WriteFile** | 10 | 1 | 9 | 10% |
| **TOTAL** | **80** | **17** | **63** | **21%** |

### Key Findings
- **Most Successful Tool:** ListDirectory (64% pass rate) - tool works well for informational operations
- **Most Problematic Tools:** CopyFile (0%), WriteFile (10%), ReadFile (11%), MoveFile (11%) - operations requiring file modifications
- **Common Failure Pattern:** LLM not invoking tools (63% of failures show "Tool was invoked 0 times")
- **Error Handling Issues:** Tests expecting error handling often fail because LLM refuses to call tools with invalid parameters

---

## ListDirectory Tool Tests

| Test Name | Purpose | Line | Status | Issue | Recommendation |
|-----------|---------|------|--------|-------|----------------|
| ListDirectory_BasicListing_Success | Verify agent can list all files and directories in a basic directory structure | 22 | ✅ Pass | None - test passes successfully with Real LLM | No changes needed - test is working as expected |
| ListDirectory_RecursiveListing_Success | Verify agent can recursively list all files in nested directory structure | 42 | ✅ Pass | None - test passes successfully with Real LLM | No changes needed - test is working as expected |
| ListDirectory_WithPattern_FiltersCorrectly | Verify agent can filter directory listing by pattern (e.g., *.txt) | 61 | ✅ Pass | None - test passes successfully with Real LLM | No changes needed - test is working as expected |
| ListDirectory_IncludeHidden_ShowsHiddenFiles | Verify agent can list hidden files when requested | 80 | ✅ Pass | None - test passes successfully with Real LLM | No changes needed - test is working as expected |
| ListDirectory_Sorted_ReturnsOrderedList | Verify agent can sort directory listing by name | 99 | ✅ Pass | None - test passes successfully with Real LLM | No changes needed - test is working as expected |
| ListDirectory_EmptyDirectory_Success | Verify agent handles empty directories gracefully | 118 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| ListDirectory_SortBySize_Success | Verify agent can sort directory listing by file size | 133 | ✅ Pass | None - test passes successfully with Real LLM | No changes needed - test is working as expected |
| ListDirectory_SortDescending_Success | Verify agent can sort directory listing in descending order | 152 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| ListDirectory_MaxDepth_Success | Verify agent respects max depth limit in recursive listing | 172 | ✅ Pass | None - test passes successfully with Real LLM | No changes needed - test is working as expected |
| ListDirectory_DirectoryNotFound_HandlesError | Verify agent handles non-existent directory error gracefully | 191 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |
| ListDirectory_InvalidPath_HandlesError | Verify agent handles invalid path error gracefully (empty string) | 205 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |

---

## CopyFile Tool Tests

| Test Name | Purpose | Line | Status | Issue | Recommendation |
|-----------|---------|------|--------|-------|----------------|
| CopyFile_BasicCopy_Success | Verify agent can perform basic file copy operation | 22 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| CopyFile_WithOverwrite_Success | Verify agent can overwrite existing files when explicitly instructed | 52 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| CopyFile_RecursiveDirectory_Success | Verify agent can recursively copy entire directory structures | 77 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| CopyFile_ToDirectory_PreservesFilename | Verify agent preserves filename when copying to directory with trailing separator | 112 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| CopyFile_WithTrailingSeparator_Success | Verify agent handles destination paths with trailing separators correctly | 137 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| CopyFile_PreserveTimestamps_Success | Verify agent preserves file timestamps when requested | 162 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| CopyFile_FollowSymlinks_Success | Verify agent follows symbolic links when copying | 194 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| CopyFile_EmptyDirectory_Success | Verify agent can copy empty directories | 219 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| CopyFile_ExcludePatterns_FiltersFiles | Verify agent can exclude files matching patterns during copy | 243 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| CopyFile_ExcludeDirectories_FiltersDirectories | Verify agent can exclude entire directories during recursive copy | 272 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| CopyFile_CreateDestinationDirectory_CreatesPath | Verify agent creates destination directory structure if it doesn't exist | 303 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| CopyFile_OverwriteDisabled_Fails | Verify agent respects overwrite=false setting and fails appropriately | 332 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| CopyFile_SourceNotFound_HandlesError | Verify agent handles missing source file error gracefully | 357 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |
| CopyFile_InvalidSourcePath_HandlesError | Verify agent handles invalid source path (empty string) gracefully | 371 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |
| CopyFile_InvalidDestinationPath_HandlesError | Verify agent handles invalid destination path gracefully | 385 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |
| CopyFile_MissingRequiredParameter_HandlesError | Verify agent handles missing required parameters error | 400 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |
| CopyFile_PathTraversalSecurity_Prevented | Verify security: agent cannot copy files outside allowed paths | 415 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |

---

## DeleteFile Tool Tests

| Test Name | Purpose | Line | Status | Issue | Recommendation |
|-----------|---------|------|--------|-------|----------------|
| DeleteFile_BasicDeletion_Success | Verify agent can delete a single file | 22 | ✅ Pass | None - test passes successfully with Real LLM | No changes needed - test is working as expected |
| DeleteFile_RecursiveDirectory_Success | Verify agent can recursively delete directories with contents | 46 | ✅ Pass | None - test passes successfully with Real LLM | No changes needed - test is working as expected |
| DeleteFile_EmptyDirectory_Success | Verify agent can delete empty directories | 73 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| DeleteFile_WithBackup_CreatesBackup | Verify agent creates backup before deleting when requested | 92 | ✅ Pass | None - test passes successfully with Real LLM | No changes needed - test is working as expected |
| DeleteFile_BackupDefaultLocation_Success | Verify agent creates backup in default location when no path specified | 126 | ✅ Pass | None - test passes successfully with Real LLM | No changes needed - test is working as expected |
| DeleteFile_ReadOnlyWithForce_Success | Verify agent can delete read-only files when force flag is used | 149 | ✅ Pass | None - test passes successfully with Real LLM | No changes needed - test is working as expected |
| DeleteFile_Statistics_ProvidesStats | Verify agent provides deletion statistics in response | 173 | ✅ Pass | None - test passes successfully with Real LLM | No changes needed - test is working as expected |
| DeleteFile_ReadOnlyWithoutForce_Fails | Verify agent fails gracefully when trying to delete read-only file without force | 192 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| DeleteFile_NonEmptyWithoutRecursive_Fails | Verify agent fails when trying to delete non-empty directory without recursive flag | 218 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| DeleteFile_WithSizeLimit_Fails | Verify agent respects size limits and fails when exceeded | 242 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| DeleteFile_WithExclusionPattern_Fails | Verify agent respects exclusion patterns and fails when protected files are targeted | 266 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| DeleteFile_FileNotFound_HandlesError | Verify agent handles non-existent file error gracefully | 289 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |
| DeleteFile_InvalidPath_HandlesError | Verify agent handles invalid path (empty string) gracefully | 303 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |
| DeleteFile_MissingParameter_HandlesError | Verify agent handles missing required parameter error | 317 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |
| DeleteFile_Cancellation_GracefulHandling | Verify agent handles cancellation requests gracefully | 331 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |

---

## MoveFile Tool Tests

| Test Name | Purpose | Line | Status | Issue | Recommendation |
|-----------|---------|------|--------|-------|----------------|
| MoveFile_BasicMove_Success | Verify agent can perform basic file move operation | 22 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| MoveFile_WithOverwrite_Success | Verify agent can move file and overwrite existing destination | 50 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| MoveFile_Rename_Success | Verify agent can rename file using move operation | 78 | ✅ Pass | None - test passes successfully with Real LLM | No changes needed - test is working as expected |
| MoveFile_Directory_Success | Verify agent can move entire directories | 104 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| MoveFile_EmptyDirectory_Success | Verify agent can move empty directories | 133 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| MoveFile_WithBackup_CreatesBackup | Verify agent creates backup before overwriting destination | 154 | ❌ Fail | Tool was invoked but validation of parameters or results failed. | Review parameter validation logic in test scenarios. Ensure expected parameters match tool schema. |
| MoveFile_ReadOnly_PreservesAttributes | Verify agent preserves file attributes (read-only) when moving | 187 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| MoveFile_CreateDestinationDirectory_CreatesPath | Verify agent creates destination directory path if it doesn't exist | 210 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| MoveFile_CrossVolume_Success | Verify agent handles cross-volume moves (copy + delete) | 237 | ✅ Pass | None - test passes successfully with Real LLM | No changes needed - test is working as expected |
| MoveFile_OverwriteDisabled_Fails | Verify agent respects overwrite=false and fails appropriately | 267 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| MoveFile_ToSubdirectory_Fails | Verify agent prevents moving directory into its own subdirectory | 295 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| MoveFile_SameSourceAndDestination_Fails | Verify agent handles same source/destination error gracefully | 320 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| MoveFile_SourceNotFound_HandlesError | Verify agent handles missing source file error gracefully | 343 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |
| MoveFile_InvalidSourcePath_HandlesError | Verify agent handles invalid source path gracefully | 358 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |
| MoveFile_InvalidDestinationPath_HandlesError | Verify agent handles invalid destination path gracefully | 373 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |
| MoveFile_MissingRequiredParameter_HandlesError | Verify agent handles missing required parameter error | 388 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |
| MoveFile_Cancellation_GracefulHandling | Verify agent handles cancellation requests gracefully | 403 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| MoveFile_Statistics_ProvidesStats | Verify agent provides move statistics in response | 418 | ❌ Fail | Tool was invoked but validation of parameters or results failed. | Review parameter validation logic in test scenarios. Ensure expected parameters match tool schema. |

---

## ReadFile Tool Tests

| Test Name | Purpose | Line | Status | Issue | Recommendation |
|-----------|---------|------|--------|-------|----------------|
| ReadFile_BasicRead_Success | Verify agent can read simple text file contents | 22 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| ReadFile_JsonFile_Success | Verify agent can read JSON file contents | 44 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| ReadFile_WithDifferentEncoding_Success | Verify agent can read files with different encodings (UTF-16) | 64 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| ReadFile_SpecificLineRange_Success | Verify agent can read specific line ranges from files | 91 | ✅ Pass | None - test passes successfully with Real LLM | No changes needed - test is working as expected |
| ReadFile_WithMaxSizeLimit_Fails | Verify agent respects file size limits | 114 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| ReadFile_PathOutsideAllowed_Fails | Verify security: agent cannot read files outside allowed paths | 140 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |
| ReadFile_FileNotFound_HandlesError | Verify agent handles non-existent file error gracefully | 159 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |
| ReadFile_InvalidPath_HandlesError | Verify agent handles invalid path (empty string) gracefully | 174 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |
| ReadFile_MissingRequiredParameter_HandlesError | Verify agent handles missing required parameter error | 189 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |

---

## WriteFile Tool Tests

| Test Name | Purpose | Line | Status | Issue | Recommendation |
|-----------|---------|------|--------|-------|----------------|
| WriteFile_BasicWrite_Success | Verify agent can write content to a new file | 22 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| WriteFile_AppendMode_Success | Verify agent can append content to existing file | 48 | ✅ Pass | None - test passes successfully with Real LLM | No changes needed - test is working as expected |
| WriteFile_WithBackup_CreatesBackup | Verify agent creates backup before overwriting file | 78 | ❌ Fail | Tool was invoked but validation of parameters or results failed. | Review parameter validation logic in test scenarios. Ensure expected parameters match tool schema. |
| WriteFile_CreateParentDirectories_CreatesPath | Verify agent creates parent directory structure when writing | 111 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| WriteFile_WithDifferentEncoding_Success | Verify agent can write files with different encodings (UTF-16) | 136 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| WriteFile_OverwriteDisabled_Fails | Verify agent respects overwrite=false and fails appropriately | 166 | ❌ Fail | LLM did not invoke the tool. The LLM likely interpreted the task but did not recognize it should use the tool. | Improve tool descriptions and examples in system prompt. Consider adding explicit instructions for when to use this tool. |
| WriteFile_PathOutsideAllowed_Fails | Verify security: agent cannot write files outside allowed paths | 194 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |
| WriteFile_InvalidEncoding_Fails | Verify agent handles invalid encoding parameter | 214 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |
| WriteFile_InvalidPath_HandlesError | Verify agent handles invalid path (empty string) gracefully | 229 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |
| WriteFile_MissingRequiredParameters_HandlesError | Verify agent handles missing required parameter error | 244 | ❌ Fail | Test expects LLM to attempt operation with invalid parameters, but LLM refuses or validates parameters before calling tool. | Revise test expectations - LLM behavior of validating before calling may be more desirable than blindly calling with invalid params. Or adjust prompts to encourage attempting operations. |

---

## 🔧 Common Issues and Fixes

Based on the test results, here are the most significant issues and recommended solutions:

### Issue 1: Tool Not Invoked (Most Common - ~70% of failures)
**Symptoms:** Test expects tool call, but LLM responds with text explanation instead of invoking the tool
**Affected Tools:** CopyFile (most severe), MoveFile, WriteFile, ReadFile (basic operations)
**Root Cause:** LLM doesn't recognize when to use file system tools, especially for modification operations
**Recommendations:**
1. **Enhance tool descriptions**: Make tool purposes more explicit and add concrete usage examples
2. **Improve system prompts**: Add explicit instructions like "When user asks to copy/move/write files, use the [tool_name] tool"
3. **Add few-shot examples**: Include examples of correct tool usage in the system prompt
4. **Consider prompt engineering**: Test if different phrasing of task descriptions improves tool recognition

### Issue 2: Error Handling Test Failures (~30% of failures)
**Symptoms:** Tests expecting error handling fail because LLM validates parameters before calling tools
**Affected Scenarios:** InvalidPath, MissingParameter, FileNotFound, PathOutsideAllowed tests
**Root Cause:** LLM performs pre-validation and refuses to call tools with obviously invalid parameters
**Philosophical Question:** Is this actually desirable behavior? The LLM is being "smart" by not making calls it knows will fail.
**Recommendations:**
1. **Reconsider test expectations**: Should tests expect attempted tool calls with invalid params, or is validation-before-call acceptable?
2. **Adjust prompts for testing**: Add instructions encouraging "attempt operations even if they might fail"
3. **Separate validation tests**: Create distinct test scenarios for pre-validation vs error-handling behaviors
4. **Document expected behavior**: Clarify in requirements whether validation-before-call is acceptable

### Issue 3: Tool-Specific Problems

#### CopyFile Tool (0% success rate)
- **Most critical failure**: ALL 17 tests fail
- **Primary issue**: LLM rarely invokes copy_file tool even for basic operations
- **Hypothesis**: Tool description may not clearly convey when copying is needed vs other operations
- **Recommendation**: Priority fix - review and rewrite copy_file tool description and examples

#### Modification Tools (Write/Move - 10-11% success rate)
- **Pattern**: Basic modification operations often fail, but specialized operations sometimes work
- **Example**: WriteFile_AppendMode passes but WriteFile_BasicWrite fails
- **Hypothesis**: LLM may not recognize basic file writing as requiring a tool, but recognizes append as special
- **Recommendation**: Emphasize that ALL file modifications require tool calls, even basic writes

#### Read Tool (11% success rate)
- **Pattern**: Line-range reading works, but basic reading fails
- **Hypothesis**: LLM may assume it can access files directly for basic reads
- **Recommendation**: Explicitly state that ALL file access must go through tools, system has no direct file access

### Issue 4: ListDirectory Success Pattern
**Observation:** ListDirectory has 64% success rate - highest of all tools
**What works:** Basic listing, recursive, patterns, sorting, hidden files, max depth
**What fails:** Empty directory, error handling cases, sort descending
**Key insight:** Informational/read operations work better than modification operations
**Hypothesis:** LLM recognizes need for tools when "getting information" but not when "doing things"
**Recommendation:** Use ListDirectory success pattern as template for improving other tools

### Issue 5: DeleteFile Partial Success (40% success rate)
**What works:** Basic deletion, recursive, backups, read-only with force, statistics
**What fails:** Error handling, empty directory, edge cases
**Pattern:** Core functionality works, but edge cases and error scenarios fail
**Recommendation:** DeleteFile tool description provides good baseline - study and replicate for other tools

---

## 📚 References

- **Test Source:** `/tests/Andy.Engine.Tests/Benchmarks/FileSystem/`
- **Scenario Definitions:** `/src/Andy.Engine.Benchmarks/Scenarios/FileSystem/`
- **Tool Implementations:** `../andy-tools/src/Andy.Tools/Library/FileSystem/`
- **Configuration:** `/tests/Andy.Engine.Tests/appsettings.json`

---

**Document Version:** 1.0
**Created:** 2025-10-30
**Last Updated:** 2025-10-30
