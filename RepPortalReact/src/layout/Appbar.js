import React from 'react';
import { Link } from 'react-router-dom';

export default function Appbar() {
  return (
    <nav className="navbar navbar-expand-lg navbar-dark bg-primary">
      <div className="container-fluid">
        <Link className="navbar-brand" to="/">RepPortal</Link>
        <button className="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#navbarNav" aria-controls="navbarNav" aria-expanded="false" aria-label="Toggle navigation">
          <span className="navbar-toggler-icon"></span>
        </button>
        <div className="collapse navbar-collapse" id="navbarNav">
          <ul className="navbar-nav me-auto mb-2 mb-lg-0">
            <li className="nav-item">
              <Link className="nav-link" to="/">Home</Link>
            </li>
            <li className="nav-item">
              <Link className="nav-link" to="/dashboard/top-customers">Top Customers</Link>
            </li>
            <li className="nav-item">
              <Link className="nav-link" to="/open-orders">Open Orders</Link>
            </li>
          </ul>
        </div>
      </div>
    </nav>
  );
}
