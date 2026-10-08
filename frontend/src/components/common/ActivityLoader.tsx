import { useEffect, useState } from 'react';
import Loader from './Loader';
import { subscribe } from '../../services/apiActivity';

// Shows the platform's global loader while any API request is in flight. In the platform this
// bridge is wired per screen; the dev shell wires it once so every page gets it for free.
export default function ActivityLoader(): React.JSX.Element {
  const [active, setActive] = useState(0);
  useEffect(() => subscribe(setActive), []);
  return <Loader visible={active > 0} />;
}
