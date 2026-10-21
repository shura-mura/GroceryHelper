import { context, propagation, SpanStatusCode, trace } from '@opentelemetry/api';
import {
  ExportResultCode,
  W3CTraceContextPropagator,
  type ExportResult,
} from '@opentelemetry/core';
import { registerInstrumentations } from '@opentelemetry/instrumentation';
import { DocumentLoadInstrumentation } from '@opentelemetry/instrumentation-document-load';
import { resourceFromAttributes } from '@opentelemetry/resources';
import {
  SimpleSpanProcessor,
  StackContextManager,
  WebTracerProvider,
  type ReadableSpan,
  type SpanExporter,
} from '@opentelemetry/sdk-trace-web';

const instrumentationName = 'GroceryHelper.Web';
const diagnosticPrefix = 'GroceryHelper browser telemetry ';
const flushTimeoutMilliseconds = 250;
const service = {
  name: instrumentationName,
  version: __BUILD_VERSION__,
  environment: __DEPLOYMENT_ENVIRONMENT__,
};
const isDevelopmentLike = ['development', 'testing'].includes(service.environment.toLowerCase());

class BrowserDiagnosticExporter implements SpanExporter {
  export(spans: ReadableSpan[], resultCallback: (result: ExportResult) => void): void {
    for (const span of spans) {
      const context = span.spanContext();
      console.debug(`${diagnosticPrefix}${JSON.stringify({
        service,
        span: {
          name: span.name,
          traceId: context.traceId,
          spanId: context.spanId,
          status: span.status.code,
          events: span.events.map((event) => ({ name: event.name })),
        },
      })}`);
    }
    resultCallback({ code: ExportResultCode.SUCCESS });
  }

  forceFlush(): Promise<void> {
    return Promise.resolve();
  }

  shutdown(): Promise<void> {
    return Promise.resolve();
  }
}

const provider = new WebTracerProvider({
  resource: resourceFromAttributes({
    'service.name': service.name,
    'service.version': service.version,
    'deployment.environment.name': service.environment,
  }),
  spanProcessors: [new SimpleSpanProcessor(new BrowserDiagnosticExporter())],
});

provider.register({
  contextManager: new StackContextManager(),
  propagator: new W3CTraceContextPropagator(),
});
registerInstrumentations({
  tracerProvider: provider,
  instrumentations: [new DocumentLoadInstrumentation()],
});

const tracer = trace.getTracer(instrumentationName);
const browserFetch = window.fetch.bind(window);

window.addEventListener('error', () => recordBrowserFailure('browser.uncaught_error'));
window.addEventListener('unhandledrejection', () => {
  recordBrowserFailure('browser.unhandled_rejection');
});
window.addEventListener('pagehide', () => {
  void flushBrowserTelemetry();
});

window.fetch = (input: RequestInfo | URL, init?: RequestInit) => {
  const request = new Request(input, init);
  const url = new URL(request.url);

  if (url.origin !== window.location.origin) {
    return browserFetch(input, init);
  }

  const route = telemetryRoute(url.pathname);

  return tracer.startActiveSpan(`${request.method} ${route}`, async (span) => {
    try {
      if (route === '/api' || (route === '/health' && isDevelopmentLike)) {
        const headers = new Headers(request.headers);
        propagation.inject(context.active(), headers, {
          set(carrier, key, value) {
            carrier.set(key, value);
          },
        });
        return await browserFetch(new Request(request, { headers }));
      }

      return await browserFetch(request);
    } finally {
      span.end();
    }
  });
};

function telemetryRoute(pathname: string): string {
  if (pathname === '/health') return '/health';
  if (pathname === '/api' || pathname.startsWith('/api/')) return '/api';
  return 'same-origin';
}

function recordBrowserFailure(name: string): void {
  const span = tracer.startSpan(name);
  span.setStatus({ code: SpanStatusCode.ERROR });
  span.addEvent('exception');
  span.end();
}

async function flushBrowserTelemetry(): Promise<void> {
  await Promise.race([
    provider.forceFlush().catch(() => undefined),
    new Promise<void>((resolve) => {
      window.setTimeout(resolve, flushTimeoutMilliseconds);
    }),
  ]);
}
