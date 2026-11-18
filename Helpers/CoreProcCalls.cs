using DigitalPaymentSystem_ILTC.Constants;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;
namespace DigitalPaymentSystem_ILTC.Helpers
{
    public class CoreProcCalls
    {
        private static readonly CloudWatchLogs objLog = new CloudWatchLogs();

        public DataRow TravelPFProcCall(string policyNumber)
        {
            DataSet ds = new DataSet();
            OracleParameter[] oracleParameters = new OracleParameter[4];
            DataRow dr;

            try
            {
                oracleParameters[0] = new OracleParameter { ParameterName = "V_POLICY_NO", Direction = ParameterDirection.Input, OracleDbType = OracleDbType.Varchar2, Value = string.IsNullOrEmpty(policyNumber) ? string.Empty : policyNumber };
                oracleParameters[1] = new OracleParameter { ParameterName = "V_PASSPORT_NO", Direction = ParameterDirection.Input, OracleDbType = OracleDbType.Varchar2 };
                oracleParameters[2] = new OracleParameter { ParameterName = "V_ERR_MSG", Direction = ParameterDirection.Output, OracleDbType = OracleDbType.Varchar2 };
                oracleParameters[3] = new OracleParameter { ParameterName = "V_OUT", Direction = ParameterDirection.Output, OracleDbType = OracleDbType.RefCursor };

                ds = ExecuteDataSetPF(ApplicationConfig.PATH_FINDER_TRAVEL_SP, oracleParameters);

                if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow row in ds.Tables[0].Select("IS_CANCELLED = 'Y'"))
                    {
                        ds.Tables[0].Rows.Remove(row);
                    }
                    ds.Tables[0].AcceptChanges();
                    dr = ds.Tables[0].Rows[0];
                    return dr;
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "CoreProcCalls.cs - TravelPFProcCall() ",
                        TraceId: null,
                        Message: "Failed to fetch policy details for Policy Number: " + policyNumber), ApplicationConfig.CORE_PROC_CALLS_LOGS);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CoreProcCalls.cs - TravelPFProcCall() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CORE_PROC_CALLS_LOGS);
            }

            return null;
        }

        public DataRow MotorPFProcCall(string policyNumber)
        {
            DataSet ds = new DataSet();
            OracleParameter[] oracleParameters = new OracleParameter[6];
            DataRow dr;

            try
            {
                oracleParameters[0] = new OracleParameter { ParameterName = "V_POLICY_NO", Direction = ParameterDirection.Input, OracleDbType = OracleDbType.Varchar2, Value = string.IsNullOrEmpty(policyNumber) ? string.Empty : policyNumber };
                oracleParameters[1] = new OracleParameter { ParameterName = "V_REGN_NO", Direction = ParameterDirection.Input, OracleDbType = OracleDbType.Varchar2 };
                oracleParameters[2] = new OracleParameter { ParameterName = "V_CHASIS_NO", Direction = ParameterDirection.Input, OracleDbType = OracleDbType.Varchar2 };
                oracleParameters[3] = new OracleParameter { ParameterName = "V_ENGINE_NO", Direction = ParameterDirection.Input, OracleDbType = OracleDbType.Varchar2 };
                oracleParameters[4] = new OracleParameter { ParameterName = "V_ERR_MSG", Direction = ParameterDirection.Output, OracleDbType = OracleDbType.Varchar2 };
                oracleParameters[5] = new OracleParameter { ParameterName = "V_OUT", Direction = ParameterDirection.Output, OracleDbType = OracleDbType.RefCursor };

                ds = ExecuteDataSetPF(ApplicationConfig.PATH_FINDER_MOTOR_SP, oracleParameters);
                
                if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow row in ds.Tables[0].Select("IS_CANCELLED = 'Y'"))
                    {
                        ds.Tables[0].Rows.Remove(row);
                    }
                    ds.Tables[0].AcceptChanges();
                    dr = ds.Tables[0].Rows[0];
                    return dr;
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "CoreProcCalls.cs - MotorPFProcCall() ",
                        TraceId: null,
                        Message: "Failed to fetch policy details for Policy Number: " + policyNumber), ApplicationConfig.CORE_PROC_CALLS_LOGS);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CoreProcCalls.cs - MotorPFProcCall() ", 
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}", 
                    StackTrace: ex.StackTrace), ApplicationConfig.CORE_PROC_CALLS_LOGS);
            }

            return null;
        }

        public DataRow HealthPFProcCall(string policyNumber)
        {
            DataSet ds = new DataSet();
            OracleParameter[] oracleParameters = new OracleParameter[6];
            DataRow dr;

            try
            {
                oracleParameters[0] = new OracleParameter { ParameterName = "V_COMPANY_NAME", Direction = ParameterDirection.Input, OracleDbType = OracleDbType.Varchar2 };
                oracleParameters[1] = new OracleParameter { ParameterName = "V_EMP_ID", Direction = ParameterDirection.Input, OracleDbType = OracleDbType.Varchar2 };
                oracleParameters[2] = new OracleParameter { ParameterName = "V_POLICY_NO", Direction = ParameterDirection.Input, OracleDbType = OracleDbType.Varchar2, Value = string.IsNullOrEmpty(policyNumber) ? string.Empty : policyNumber };
                oracleParameters[3] = new OracleParameter { ParameterName = "V_UHID", Direction = ParameterDirection.Input, OracleDbType = OracleDbType.Varchar2 };
                oracleParameters[4] = new OracleParameter { ParameterName = "V_ERR_MSG", Direction = ParameterDirection.Output, OracleDbType = OracleDbType.Varchar2 };
                oracleParameters[5] = new OracleParameter { ParameterName = "V_OUT", Direction = ParameterDirection.Output, OracleDbType = OracleDbType.RefCursor };

                ds = ExecuteDataSetPF(ApplicationConfig.PATH_FINDER_HEALTH_SP, oracleParameters);

                if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow row in ds.Tables[0].Select("IS_CANCELLED = 'Y'"))
                    {
                        ds.Tables[0].Rows.Remove(row);
                    }
                    ds.Tables[0].AcceptChanges();
                    dr = ds.Tables[0].Rows[0];
                    return dr;
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "CoreProcCalls.cs - HealthPFProcCall() ",
                        TraceId: null,
                        Message: "Failed to fetch policy details for Policy Number: " + policyNumber), ApplicationConfig.CORE_PROC_CALLS_LOGS);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CoreProcCalls.cs - HealthPFProcCall() ", 
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}", 
                    StackTrace: ex.StackTrace), ApplicationConfig.CORE_PROC_CALLS_LOGS);
            }

            return null;
        }

        public DataRow HomePFProcCall(string policyNumber)
        {
            DataSet ds = new DataSet();
            OracleParameter[] oracleParameters = new OracleParameter[3];
            DataRow dr;

            try
            {
                oracleParameters[0] = new OracleParameter { ParameterName = "V_POLICY_NO", Direction = ParameterDirection.Input, OracleDbType = OracleDbType.Varchar2, Value = string.IsNullOrEmpty(policyNumber) ? string.Empty : policyNumber };
                oracleParameters[1] = new OracleParameter { ParameterName = "V_ERR_MSG", Direction = ParameterDirection.Output, OracleDbType = OracleDbType.Varchar2 };
                oracleParameters[2] = new OracleParameter { ParameterName = "V_OUT", Direction = ParameterDirection.Output, OracleDbType = OracleDbType.RefCursor };

                ds = ExecuteDataSetPF(ApplicationConfig.PATH_FINDER_HOME_SP, oracleParameters);
               
                if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow row in ds.Tables[0].Select("IS_CANCELLED = 'Y'"))
                    {
                        ds.Tables[0].Rows.Remove(row);
                    }
                    ds.Tables[0].AcceptChanges();
                    dr = ds.Tables[0].Rows[0];
                    return dr;
                }
                else
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                        MethodName: "CoreProcCalls.cs - HomePFProcCall() ",
                        TraceId: null,
                        Message: "Failed to fetch policy details for Policy Number: " + policyNumber), ApplicationConfig.CORE_PROC_CALLS_LOGS);
                }
            }
            catch (Exception ex)
            {
                objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CoreProcCalls.cs - HomePFProcCall() ", 
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}", 
                    StackTrace: ex.StackTrace), ApplicationConfig.CORE_PROC_CALLS_LOGS);
            }

            return null;
        }

        public static DataSet ExecuteDataSetPF(string procedureName, OracleParameter[] parameter, int timeout = 0)
        {            
            DataSet dataSet = new DataSet();
            using (OracleConnection oracleConnection = new OracleConnection(ApplicationConfig.ORACLE_PATH_FINDER))
            {
                if (oracleConnection.State != ConnectionState.Open)
                {
                    oracleConnection.Open();
                }

                try
                {
                    using OracleCommand oracleCommand = new OracleCommand(procedureName, oracleConnection);
                    oracleCommand.CommandTimeout = timeout;
                    using OracleDataAdapter oracleDataAdapter = new OracleDataAdapter(oracleCommand);
                    oracleCommand.CommandType = CommandType.StoredProcedure;
                    if (parameter != null)
                    {
                        OracleParameter oracleParameter = new OracleParameter();
                        foreach (OracleParameter oracleParameter2 in parameter)
                        {
                            oracleParameter = new OracleParameter();
                            oracleParameter.ParameterName = oracleParameter2.ParameterName;
                            oracleParameter.OracleDbType = oracleParameter2.OracleDbType;
                            oracleParameter.Direction = oracleParameter2.Direction;
                            oracleParameter.Value = oracleParameter2.Value;
                            oracleCommand.Parameters.Add(oracleParameter);
                        }
                    }

                    oracleDataAdapter.Fill(dataSet);
                }
                catch (Exception ex)
                {
                    objLog.LogAWSMessageAsync(objLog.GenericAWSException_CWL(
                    MethodName: "CoreProcCalls.cs - ExecuteDataSetPF() ",
                    TraceId: null,
                    Message: $"Exception occurred: {ex.Message}",
                    StackTrace: ex.StackTrace), ApplicationConfig.CORE_PROC_CALLS_LOGS);
                }
            }

            return dataSet;
        }
    }
}
