public readonly struct ApiResult<T>
{
    public readonly bool Ok;
    public readonly int StatusCode;
    public readonly string Error;
    public readonly T Data;

    public ApiResult(bool ok, int statusCode, string error, T data)
    {
        Ok = ok;
        StatusCode = statusCode;
        Error = error;
        Data = data;
    }
}
