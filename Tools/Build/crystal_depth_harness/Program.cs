// Runs every [Test] in the compiled shipped suites. Exit code = number of failures.
public static class Program
{
    public static int Main(string[] args) => new NUnitLite.AutoRun(typeof(Program).Assembly).Execute(args);
}
