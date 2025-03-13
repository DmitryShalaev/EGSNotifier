#if !DEBUG
using System.Net;
using System.Net.Mail;
#endif

namespace Core.Bot {
    public static class ErrorReport {

        public static async Task Send(string msg, Exception e) {
            await Console.Out.WriteLineAsync($"{msg}\n{new('-', 25)}");
            await Console.Out.WriteLineAsync($"{e.Message}\n{new('-', 25)}\n{e}");
        }
    }
}