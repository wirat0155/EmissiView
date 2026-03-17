using EmissiView.Models;
using EmissiView.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EmissiView.Repositories
{
    public class EmpRepository
    {
        private readonly DapperService _dapperService;

        public EmpRepository(DapperService dapperService)
        {
            _dapperService = dapperService;
        }

        public async Task<(bool success, string? empNo, string? message)> LoginAsync(string username, string password)
        {
            try
            {
                // Check password
                string pwdSql = $@"SELECT [username] FROM [vw_username_subcon] WHERE [username] = @u AND [userpasshash] = HASHBYTES('SHA', '{password}')";
                var userCheck = await _dapperService.QueryFirst<string>("UICT", pwdSql, new { u = username });

                if (string.IsNullOrEmpty(userCheck))
                {
                    return (false, null, "Invalid username or password");
                }

                // Check employee status
                string statusSql = "SELECT empstatusno FROM vw_emp WHERE empno = @Username AND empstatusno = 'N'";
                var empStatus = await _dapperService.QueryFirst<string>("UICT", statusSql, new { Username = username });

                if (empStatus == "R")
                {
                    return (false, username, "Your account is inactive. Please contact HR department.");
                }

                return (true, username, null);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error during login: {ex.Message}", ex);
            }
        }
    }
}