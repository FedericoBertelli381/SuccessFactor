namespace SuccessFactor.Goals.Importing;

public class BatchResultDto
{
    public int Total { get; set; }
    public int Ok { get; set; }
    public int Error { get; set; }
    public string Status { get; set; } = default!;
}