import React from 'react';
import { Routes, Route, Link } from 'react-router-dom';
import TopCustomers from './components/TopCustomers';
import Home from './components/Home';
import OpenOrders from './components/OpenOrders';
import Appbar from './layout/Appbar';
import Footer from './layout/Footer';

export default function App() {
  return (
    <div className="d-flex flex-column min-vh-100">
      <Appbar />
      <main className="flex-grow-1 container py-3">
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/dashboard/top-customers" element={<TopCustomers />} />
          <Route path="/open-orders" element={<OpenOrders />} />
        </Routes>
      </main>
      <Footer />
    </div>
  );
}
