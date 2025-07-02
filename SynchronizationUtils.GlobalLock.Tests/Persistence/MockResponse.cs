using Azure;
using Azure.Core;

namespace SynchronizationUtils.GlobalLock.Tests.Persistence
{
    internal sealed class MockResponse(int status, string reasonPhrase = null) : Response
    {
        private readonly Dictionary<string, List<string>> _headers = new(StringComparer.OrdinalIgnoreCase);
        private bool? _isError;

        public static Response NoContent { get; } = new MockResponse(204, "No Content");

        public static Response PreconditionFailed { get; } = new MockResponse(412, "Precondition Failed");

        public override int Status { get; } = status;

        public override string ReasonPhrase { get; } = reasonPhrase;

        public override Stream ContentStream { get; set; }

        public override string ClientRequestId { get; set; }

        public override bool IsError { get => _isError ?? base.IsError; }

        public void SetIsError(bool value) => _isError = value;

        public bool IsDisposed { get; private set; }

        protected override bool TryGetHeader(string name, out string value)
        {
            if (_headers.TryGetValue(name, out List<string> values))
            {
                value = JoinHeaderValue(values);
                return true;
            }

            value = null;
            return false;
        }

        protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values)
        {
            var result = _headers.TryGetValue(name, out List<string> valuesList);
            values = valuesList;
            return result;
        }

        protected override bool ContainsHeader(string name)
        {
            return TryGetHeaderValues(name, out _);
        }

        protected override IEnumerable<HttpHeader> EnumerateHeaders()
        {
            return _headers.Select(h => new HttpHeader(h.Key, JoinHeaderValue(h.Value)));
        }

        private static string JoinHeaderValue(IEnumerable<string> values)
        {
            return string.Join(",", values);
        }

        public override void Dispose()
        {
            IsDisposed = true;
        }
    }
}
