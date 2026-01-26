using CarrotDownload.Database.Models;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using BCrypt.Net;
using MongoDB.Bson;

namespace CarrotDownload.Database
{
    public class CarrotMongoService
    {
        public enum LoginFailureReason
        {
            Unknown = 0,
            UserNotFound = 1,
            WrongPassword = 2,
            UserBanned = 3,
            DeviceBanned = 4,
            DeviceMismatch = 5
        }

        public sealed record LoginAttemptResult(UserModel? User, LoginFailureReason? FailureReason);

        private readonly IMongoCollection<ProgramModel> _programs;
        private readonly IMongoCollection<ProjectModel> _projects;
        private readonly IMongoCollection<UserModel> _users;
        private readonly IMongoCollection<PlaylistModel> _playlists;
        private readonly IMongoCollection<ProgrammingFileModel> _programmingFiles;
        private readonly IMongoCollection<ExportHistoryModel> _exportHistory;
        private readonly IMongoCollection<MacIdLogModel> _macIdLogs;
        private readonly IMongoCollection<AdminModel> _admins;

        public CarrotMongoService(string connectionString)
        {
            try
            {
                var client = new MongoClient(connectionString);
                var appDatabase = client.GetDatabase("CarrotDB");
                var websiteDatabase = client.GetDatabase("test");

                _programs = appDatabase.GetCollection<ProgramModel>("Programs");
                _projects = appDatabase.GetCollection<ProjectModel>("Projects");
                _users = websiteDatabase.GetCollection<UserModel>("video_hero_users");
                _playlists = appDatabase.GetCollection<PlaylistModel>("Playlists");
                _programmingFiles = appDatabase.GetCollection<ProgrammingFileModel>("ProgrammingFiles");
                _exportHistory = appDatabase.GetCollection<ExportHistoryModel>("ExportHistory");
                _macIdLogs = appDatabase.GetCollection<MacIdLogModel>("MacIdLogs");
                _admins = appDatabase.GetCollection<AdminModel>("Admins");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FATAL] MongoDB Connection failed: {ex.Message}");
                throw;
            }
        }

        // User Authentication Operations
        public async Task<UserModel?> RegisterUserAsync(string fullName, string email, string password, string deviceId)
        {
            // Check if user already exists
            var existingUser = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
            if (existingUser != null)
            {
                return null; // User already exists
            }

            var user = new UserModel
            {
                FullName = fullName,
                Email = email,
                PasswordHash = HashPassword(password),
                DeviceId = deviceId,
                CreatedAt = DateTime.UtcNow
            };

            await _users.InsertOneAsync(user);
            return user;
        }

        public async Task<UserModel?> LoginUserAsync(string email, string password, string deviceId)
        {
            var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
            
            if (user == null || !VerifyPassword(password, user.PasswordHash))
            {
                return null; // Invalid credentials
            }

            // Check if user is banned
            if (user.IsBanned)
            {
                Console.WriteLine($"[LOGIN] User {email} is BANNED");
                return null;
            }

            // Check if MAC ID is banned
            var isMacBanned = await IsMacIdBannedAsync(deviceId);
            if (isMacBanned)
            {
                Console.WriteLine($"[LOGIN] MAC ID {deviceId} is BANNED");
                return null;
            }

            // Device Binding Logic with detailed logging
            Console.WriteLine($"[LOGIN] Current Device ID: {deviceId}");
            Console.WriteLine($"[LOGIN] Stored Device ID: {user.DeviceId ?? "NULL"}");
            Console.WriteLine($"[LOGIN] User Whitelist Status: {user.IsWhitelisted}");
            
            // 1. If user doesn't have a DeviceId, capture it now (legacy users or first login)
            if (string.IsNullOrEmpty(user.DeviceId))
            {
                Console.WriteLine("[LOGIN] No device ID stored - binding to current device for future tracking");
                user.DeviceId = deviceId;
                var update = Builders<UserModel>.Update.Set(u => u.DeviceId, deviceId);
                await _users.UpdateOneAsync(u => u.Id == user.Id, update);
            }

            // 2. Performance device binding check
            if (user.IsWhitelisted)
            {
                Console.WriteLine("[LOGIN] User is WHITELISTED - bypassing device mismatch check");
            }
            else if (user.DeviceId != deviceId)
            {
                // Device mismatch - REJECT login
                Console.WriteLine($"[LOGIN] DEVICE MISMATCH! Expected: {user.DeviceId}, Got: {deviceId}");
                Console.WriteLine("[LOGIN] Login REJECTED due to device mismatch");
                return null; 
            }
            else
            {
                Console.WriteLine("[LOGIN] Device ID matches - login allowed");
            }

            // Log MAC ID usage
            await LogMacIdUsageAsync(deviceId, user.Id, user.Email, user.FullName);

            // Update last login time and capture device ID if newly bound
            var loginUpdate = Builders<UserModel>.Update
                .Set(u => u.LastLoginAt, DateTime.UtcNow)
                .Set(u => u.DeviceId, user.DeviceId);
            await _users.UpdateOneAsync(u => u.Id == user.Id, loginUpdate);

            return user;
        }

        /// <summary>
        /// Same as <see cref="LoginUserAsync"/>, but returns a failure reason so the UI can show a more helpful message.
        /// </summary>
        public async Task<LoginAttemptResult> LoginUserDetailedAsync(string email, string password, string deviceId)
        {
            var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();

            if (user == null)
            {
                return new LoginAttemptResult(null, LoginFailureReason.UserNotFound);
            }

            if (!VerifyPassword(password, user.PasswordHash))
            {
                return new LoginAttemptResult(null, LoginFailureReason.WrongPassword);
            }

            // Check if user is banned
            if (user.IsBanned)
            {
                Console.WriteLine($"[LOGIN] User {email} is BANNED");
                return new LoginAttemptResult(null, LoginFailureReason.UserBanned);
            }

            // Check if MAC ID is banned
            var isMacBanned = await IsMacIdBannedAsync(deviceId);
            if (isMacBanned)
            {
                Console.WriteLine($"[LOGIN] MAC ID {deviceId} is BANNED");
                return new LoginAttemptResult(null, LoginFailureReason.DeviceBanned);
            }

            // Device Binding Logic with detailed logging
            Console.WriteLine($"[LOGIN] Current Device ID: {deviceId}");
            Console.WriteLine($"[LOGIN] Stored Device ID: {user.DeviceId ?? "NULL"}");
            Console.WriteLine($"[LOGIN] User Whitelist Status: {user.IsWhitelisted}");

            // 1. If user doesn't have a DeviceId, capture it now (legacy users or first login)
            if (string.IsNullOrEmpty(user.DeviceId))
            {
                Console.WriteLine("[LOGIN] No device ID stored - binding to current device for future tracking");
                user.DeviceId = deviceId;
                var update = Builders<UserModel>.Update.Set(u => u.DeviceId, deviceId);
                await _users.UpdateOneAsync(u => u.Id == user.Id, update);
            }

            // 2. Performance device binding check
            if (user.IsWhitelisted)
            {
                Console.WriteLine("[LOGIN] User is WHITELISTED - bypassing device mismatch check");
            }
            else if (user.DeviceId != deviceId)
            {
                // Device mismatch - REJECT login
                Console.WriteLine($"[LOGIN] DEVICE MISMATCH! Expected: {user.DeviceId}, Got: {deviceId}");
                Console.WriteLine("[LOGIN] Login REJECTED due to device mismatch");
                return new LoginAttemptResult(null, LoginFailureReason.DeviceMismatch);
            }
            else
            {
                Console.WriteLine("[LOGIN] Device ID matches - login allowed");
            }

            // Log MAC ID usage
            await LogMacIdUsageAsync(deviceId, user.Id, user.Email, user.FullName);

            // Update last login time and capture device ID if newly bound
            var loginUpdate = Builders<UserModel>.Update
                .Set(u => u.LastLoginAt, DateTime.UtcNow)
                .Set(u => u.DeviceId, user.DeviceId);
            await _users.UpdateOneAsync(u => u.Id == user.Id, loginUpdate);

            return new LoginAttemptResult(user, null);
        }

        public async Task<bool> LoginAdminInDbAsync(string username, string password)
        {
            var admin = await _admins.Find(a => a.Username == username).FirstOrDefaultAsync();
            if (admin == null) return false;

            return VerifyPassword(password, admin.PasswordHash);
        }

        public async Task CreateAdminAsync(string username, string password)
        {
            var admin = new AdminModel
            {
                Username = username,
                PasswordHash = HashPassword(password)
            };
            await _admins.InsertOneAsync(admin);
        }

        public async Task<UserModel?> GetUserByIdAsync(string userId)
        {
            return await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        }

        public async Task<UserModel?> GetUserByEmailAsync(string email)
        {
            return await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
        }

        /// <summary>
        /// Get the users collection (for sync service)
        /// </summary>
        public IMongoCollection<UserModel> GetUsersCollection()
        {
            return _users;
        }

        /// <summary>
        /// Create a new user (for sync service)
        /// </summary>
        public async Task CreateUserAsync(UserModel user)
        {
            await _users.InsertOneAsync(user);
        }

        // Password hashing utilities (using BCrypt for live website compatibility)
        private string HashPassword(string password)
        {
            try {
                return BCrypt.Net.BCrypt.HashPassword(password);
            } catch {
                // Last resort fallback if BCrypt fails for some reason
                using var sha256 = SHA256.Create();
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrEmpty(hash)) return false;

            try
            {
                // Fallback for SHA256 (if needed during transition or for old admins)
                // BCrypt hashes always start with $2
                if (!hash.StartsWith("$2"))
                {
                    using var sha256 = SHA256.Create();
                    var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                    var sha256Hash = Convert.ToBase64String(hashedBytes);
                    return sha256Hash == hash;
                }

                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VERIFY] Password verification failed: {ex.Message}");
                // Double check if it's actually an old SHA256 that didn't match the condition above
                try {
                    using var sha256 = SHA256.Create();
                    var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                    var sha256Hash = Convert.ToBase64String(hashedBytes);
                    return sha256Hash == hash;
                } catch {
                    return false;
                }
            }
        }

        // Program Operations
        public async Task CreateProgramAsync(ProgramModel program)
        {
            await _programs.InsertOneAsync(program);
        }

        public async Task<List<ProgramModel>> GetUserProgramsAsync(string userId)
        {
            return await _programs.Find(p => p.UserId == userId).ToListAsync();
        }

        // Project Operations
        public async Task CreateProjectAsync(ProjectModel project)
        {
            await _projects.InsertOneAsync(project);
        }

        public async Task<List<ProjectModel>> GetUserProjectsAsync(string userId)
        {
            return await _projects.Find(p => p.UserId == userId).ToListAsync();
        }

        public async Task<ProjectModel?> GetProjectByIdAsync(string projectId)
        {
            return await _projects.Find(p => p.ProjectId == projectId).FirstOrDefaultAsync();
        }

        public async Task DeleteProjectAsync(string projectId)
        {
            await _projects.DeleteOneAsync(p => p.Id == projectId);
        }

        public async Task UpdateProjectFilesAsync(string projectId, List<string> files)
        {
            var filter = Builders<ProjectModel>.Filter.Eq(p => p.ProjectId, projectId);
            var update = Builders<ProjectModel>.Update.Set(p => p.Files, files);
            await _projects.UpdateOneAsync(filter, update);
        }

        // Playlist Operations
        public async Task CreatePlaylistAsync(PlaylistModel playlist)
        {
            await _playlists.InsertOneAsync(playlist);
        }

        public async Task<List<PlaylistModel>> GetProjectPlaylistsAsync(string projectId)
        {
            return await _playlists.Find(p => p.ProjectId == projectId).ToListAsync();
        }

        public async Task DeletePlaylistByFilePathAsync(string filePath)
        {
            await _playlists.DeleteOneAsync(p => p.FilePath == filePath);
        }



        public async Task UpdatePlaylistSlotAsync(string playlistId, string newSlotPosition)
        {
            var filter = Builders<PlaylistModel>.Filter.Eq(p => p.Id, playlistId);
            var update = Builders<PlaylistModel>.Update.Set(p => p.SlotPosition, newSlotPosition);
            await _playlists.UpdateOneAsync(filter, update);
        }

        public async Task UpdatePlaylistSlotAndOrderAsync(string playlistId, string newSlotPosition, int orderIndex)
        {
            var filter = Builders<PlaylistModel>.Filter.Eq(p => p.Id, playlistId);
            var update = Builders<PlaylistModel>.Update
                .Set(p => p.SlotPosition, newSlotPosition)
                .Set(p => p.OrderIndex, orderIndex);
            await _playlists.UpdateOneAsync(filter, update);
        }

        public async Task DeletePlaylistByIdAsync(string id)
        {
            await _playlists.DeleteOneAsync(p => p.Id == id);
        }

        public async Task DeleteProjectPlaylistsAsync(string projectId)
        {
            await _playlists.DeleteManyAsync(p => p.ProjectId == projectId);
        }

        public async Task UpdatePlaylistNotesAsync(string playlistId, List<string> notes)
        {
            var filter = Builders<PlaylistModel>.Filter.Eq(p => p.Id, playlistId);
            var update = Builders<PlaylistModel>.Update.Set(p => p.Notes, notes);
            await _playlists.UpdateOneAsync(filter, update);
        }

        // Programming Files Operations
        public async Task CreateProgrammingFileAsync(ProgrammingFileModel programmingFile)
        {
            await _programmingFiles.InsertOneAsync(programmingFile);
        }

        public async Task<List<ProgrammingFileModel>> GetProgrammingFilesAsync()
        {
            return await _programmingFiles.Find(_ => true).ToListAsync();
        }

        public async Task<List<ProgrammingFileModel>> GetUserProgrammingFilesAsync(string userId)
        {
            return await _programmingFiles.Find(p => p.UserId == userId).ToListAsync();
        }

        public async Task DeleteProgrammingFileAsync(string id)
        {
            await _programmingFiles.DeleteOneAsync(p => p.Id == id);
        }

        public async Task UpdateProgrammingFileSlotAsync(string id, string newSlotPosition)
        {
            var filter = Builders<ProgrammingFileModel>.Filter.Eq(p => p.Id, id);
            var update = Builders<ProgrammingFileModel>.Update.Set(p => p.SlotPosition, newSlotPosition);
            await _programmingFiles.UpdateOneAsync(filter, update);
        }

        // Export History Operations
        public async Task CreateExportHistoryAsync(ExportHistoryModel export)
        {
            await _exportHistory.InsertOneAsync(export);
        }

        public async Task<List<ExportHistoryModel>> GetUserExportHistoryAsync(string userId)
        {
            return await _exportHistory.Find(e => e.UserId == userId)
                .SortByDescending(e => e.ExportedAt)
                .ToListAsync();
        }

        public async Task DeleteExportHistoryAsync(string exportId)
        {
            await _exportHistory.DeleteOneAsync(e => e.Id == exportId);
        }

        public async Task<bool> VerifyUserPasswordAsync(string userId, string password)
        {
            var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null) return false;
            return VerifyPassword(password, user.PasswordHash);
        }

        public async Task<bool> UpdateUserBasicInfoAsync(string userId, string fullName, string email)
        {
            var update = Builders<UserModel>.Update
                .Set(u => u.FullName, fullName)
                .Set(u => u.Email, email);
            var result = await _users.UpdateOneAsync(u => u.Id == userId, update);
            return result.IsAcknowledged && result.MatchedCount > 0;
        }

        // ==================== ADMIN OPERATIONS ====================

        // Dashboard Statistics
        public async Task<int> GetTotalUsersCountAsync()
        {
            return (int)await _users.CountDocumentsAsync(_ => true);
        }

        public async Task<int> GetTotalProjectsCountAsync()
        {
            return (int)await _projects.CountDocumentsAsync(_ => true);
        }

        public async Task<int> GetTotalProgrammingFilesCountAsync()
        {
            return (int)await _programmingFiles.CountDocumentsAsync(_ => true);
        }

        public async Task<int> GetTotalPlaylistsCountAsync()
        {
            return (int)await _playlists.CountDocumentsAsync(_ => true);
        }

        // User Management
        public async Task<List<UserModel>> GetAllUsersAsync()
        {
            return await _users.Find(_ => true).ToListAsync();
        }

        public async Task<bool> BanUserAsync(string userId)
        {
            var update = Builders<UserModel>.Update
                .Set(u => u.IsBanned, true)
                .Set(u => u.BannedAt, DateTime.UtcNow);
            var result = await _users.UpdateOneAsync(u => u.Id == userId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UnbanUserAsync(string userId)
        {
            var update = Builders<UserModel>.Update
                .Set(u => u.IsBanned, false)
                .Set(u => u.BannedAt, null);
            var result = await _users.UpdateOneAsync(u => u.Id == userId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> WhitelistUserAsync(string userId, bool isWhitelisted)
        {
            var update = Builders<UserModel>.Update.Set(u => u.IsWhitelisted, isWhitelisted);
            var result = await _users.UpdateOneAsync(u => u.Id == userId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> ResetUserPasswordAsync(string userId, string newPassword)
        {
            var update = Builders<UserModel>.Update.Set(u => u.PasswordHash, HashPassword(newPassword));
            var result = await _users.UpdateOneAsync(u => u.Id == userId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> ResetUserDeviceBindingAsync(string userId)
        {
            // Clear the DeviceId field
            var update = Builders<UserModel>.Update.Set(u => u.DeviceId, null);
            var result = await _users.UpdateOneAsync(u => u.Id == userId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteUserAsync(string userId)
        {
            // IMPORTANT: DO NOT DELETE USER'S ORIGINAL FILES FROM DISK
            // Only remove database records. User files must remain untouched.
            // Physical file deletion is DISABLED to protect user data.
            
            // 1. Physical Project Files - DISABLED (preserve user files)
            // Files are stored at original locations and should never be deleted
            // try
            // {
            //     var userProjects = await _projects.Find(p => p.UserId == userId).ToListAsync();
            //     foreach (var project in userProjects)
            //     {
            //         if (!string.IsNullOrEmpty(project.StoragePath) && Directory.Exists(project.StoragePath))
            //         {
            //             Directory.Delete(project.StoragePath, recursive: true);
            //         }
            //     }
            // }
            // catch (Exception ex)
            // {
            //     Console.WriteLine($"[DeleteUser] Error processing project files: {ex.Message}");
            // }

            // 2. Physical Programming Files - DISABLED (preserve user files)
            // Files are stored at original locations and should never be deleted
            // try
            // {
            //     var userPrograms = await _programmingFiles.Find(p => p.UserId == userId).ToListAsync();
            //     var processedFolders = new HashSet<string>();
            //     foreach (var prog in userPrograms)
            //     {
            //         if (!string.IsNullOrEmpty(prog.FilePath))
            //         {
            //             var folderPath = Path.GetDirectoryName(prog.FilePath);
            //             if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath) && !processedFolders.Contains(folderPath))
            //             {
            //                 Directory.Delete(folderPath, recursive: true);
            //                 processedFolders.Add(folderPath);
            //             }
            //         }
            //     }
            // }
            // catch (Exception ex)
            // {
            //     Console.WriteLine($"[DeleteUser] Error processing programming files: {ex.Message}");
            // }

            // 3. Delete DB Records
            await _programs.DeleteManyAsync(p => p.UserId == userId);
            await _projects.DeleteManyAsync(p => p.UserId == userId);
            await _playlists.DeleteManyAsync(p => p.UserId == userId);
            await _programmingFiles.DeleteManyAsync(p => p.UserId == userId);
            await _exportHistory.DeleteManyAsync(e => e.UserId == userId);
            
            // KEEP MacIdLogs to maintain device bans even if user is deleted
            // await _macIdLogs.DeleteManyAsync(m => m.UserId == userId); 

            // Finally delete the user
            var result = await _users.DeleteOneAsync(u => u.Id == userId);
            return result.DeletedCount > 0;
        }

        // MAC ID Management
        public async Task LogMacIdUsageAsync(string macId, string userId, string userEmail, string userName)
        {
            var existingLog = await _macIdLogs.Find(m => m.MacId == macId && m.UserId == userId).FirstOrDefaultAsync();

            if (existingLog != null)
            {
                // Update existing log
                var update = Builders<MacIdLogModel>.Update
                    .Set(m => m.LastUsedAt, DateTime.UtcNow)
                    .Inc(m => m.LoginCount, 1);
                await _macIdLogs.UpdateOneAsync(m => m.Id == existingLog.Id, update);
            }
            else
            {
                // Create new log
                var newLog = new MacIdLogModel
                {
                    MacId = macId,
                    UserId = userId,
                    UserEmail = userEmail,
                    UserName = userName,
                    FirstUsedAt = DateTime.UtcNow,
                    LastUsedAt = DateTime.UtcNow,
                    LoginCount = 1
                };
                await _macIdLogs.InsertOneAsync(newLog);
            }
        }

        public async Task<List<MacIdLogModel>> GetAllMacIdLogsAsync()
        {
            return await _macIdLogs.Find(_ => true)
                .SortByDescending(m => m.LastUsedAt)
                .ToListAsync();
        }

        public async Task<bool> DeleteMacIdLogAsync(string macId)
        {
            var result = await _macIdLogs.DeleteManyAsync(m => m.MacId == macId);
            return result.DeletedCount > 0;
        }

        public async Task<bool> BanMacIdAsync(string macId)
        {
            var update = Builders<MacIdLogModel>.Update
                .Set(m => m.IsBanned, true)
                .Set(m => m.BannedAt, DateTime.UtcNow);
            var result = await _macIdLogs.UpdateManyAsync(m => m.MacId == macId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UnbanMacIdAsync(string macId)
        {
            var update = Builders<MacIdLogModel>.Update
                .Set(m => m.IsBanned, false)
                .Set(m => m.BannedAt, null);
            var result = await _macIdLogs.UpdateManyAsync(m => m.MacId == macId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> IsMacIdBannedAsync(string macId)
        {
            var log = await _macIdLogs.Find(m => m.MacId == macId && m.IsBanned).FirstOrDefaultAsync();
            return log != null;
        }

        // ==================== SHIPPING ADDRESS OPERATIONS ====================
        public async Task<bool> AddShippingAddressAsync(string userId, ShippingAddress address)
        {
            var filter = Builders<UserModel>.Filter.Eq(u => u.Id, userId);
            var update = Builders<UserModel>.Update.Push(u => u.ShippingAddresses, address);
            var result = await _users.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateShippingAddressAsync(string userId, ShippingAddress address)
        {
            var filter = Builders<UserModel>.Filter.And(
                Builders<UserModel>.Filter.Eq(u => u.Id, userId),
                Builders<UserModel>.Filter.ElemMatch(u => u.ShippingAddresses, a => a.Id == address.Id)
            );
            var update = Builders<UserModel>.Update.Set("ShippingAddresses.$", address);
            var result = await _users.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> RemoveShippingAddressAsync(string userId, string addressId)
        {
            var filter = Builders<UserModel>.Filter.Eq(u => u.Id, userId);
            var update = Builders<UserModel>.Update.PullFilter(u => u.ShippingAddresses, a => a.Id == addressId);
            var result = await _users.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<List<ShippingAddress>> GetShippingAddressesAsync(string userId)
        {
            var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            return user?.ShippingAddresses ?? new List<ShippingAddress>();
        }
    }
}
