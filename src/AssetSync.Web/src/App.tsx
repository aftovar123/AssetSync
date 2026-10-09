import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { AssetsPage } from './pages/AssetsPage'
import { OverviewPage } from './pages/OverviewPage'
import { SyncQueuePage } from './pages/SyncQueuePage'
import { WorkOrdersPage } from './pages/WorkOrdersPage'
import { OperatorSessionProvider } from './session/OperatorSessionProvider'

const queryClient = new QueryClient({
  defaultOptions: {
    // The free-tier database can take up to a minute to wake up; one retry
    // covers that without hammering the API.
    queries: { retry: 1, staleTime: 2_000 },
  },
})

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <OperatorSessionProvider>
        <BrowserRouter>
          <Routes>
            <Route element={<Layout />}>
              <Route index element={<OverviewPage />} />
              <Route path="ordenes" element={<WorkOrdersPage />} />
              <Route path="activos" element={<AssetsPage />} />
              <Route path="cola" element={<SyncQueuePage />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </OperatorSessionProvider>
    </QueryClientProvider>
  )
}
